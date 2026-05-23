// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;

using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Utils;

namespace ComicReaderUWP.Core.Common.Caching;

public class LRUCache(string directoryPath)
{
    private const string TAG = nameof(LRUCache);
    private const string DATABASE_FILE_NAME = "info.db";
    private const int BATCH_SIZE = 1000;

    private readonly string _directoryPath = directoryPath;
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = [];

    private readonly LRUCacheDatabase _database = new(Path.Combine(directoryPath, DATABASE_FILE_NAME));
    private readonly ReaderWriterLock _flushLock = new();
    private volatile ConcurrentDictionary<string, long> _pendingFlushKeys = [];
    private int _postFlushTask = 0;

    public LRUCacheStream? Get(string key)
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));

        string hashedKey = ToHashedKey(key);
        CacheEntry entry = _entries.GetOrAdd(hashedKey, key => new CacheEntry(this, key));
        LRUCacheStream? stream = entry.StartRead();
        if (stream != null)
        {
            AddPendingFlushKey(key);
        }

        return stream;
    }

    public LRUCacheStream? Put(string key)
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));

        string hashedKey = ToHashedKey(key);
        CacheEntry entry = _entries.GetOrAdd(hashedKey, key => new CacheEntry(this, key));
        LRUCacheStream? stream = entry.StartWrite();
        if (stream != null)
        {
            AddPendingFlushKey(key);
        }

        return stream;
    }

    public void Clear()
    {
        _database.Clear();
    }

    public long GetApproximateSize()
    {
        var directory = new DirectoryInfo(_directoryPath);
        return FileUtils.GetApproximateDirectorySize(directory);
    }

    public void Cleanup(long maxSize)
    {
        long sizeToRemove = GetApproximateSize() - maxSize;
        if (sizeToRemove <= 0)
        {
            return;
        }

        void DeleteFile(string path)
        {
            try
            {
                var fileInfo = new FileInfo(path);
                long fileSize = fileInfo.Length;
                File.Delete(path);
                sizeToRemove -= fileSize;
            }
            catch (IOException e)
            {
                Logger.E(TAG, e);
            }
            catch (Exception e)
            {
                Logger.F(TAG, e);
            }
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(_directoryPath);
        }
        catch (Exception ex)
        {
            Logger.F(TAG, ex);
            return;
        }

        List<Tuple<string, long>> lastUsedTimes = [];
        for (int i = 0; i < files.Length;)
        {
            Dictionary<string, string> batch = [];
            for (; i < files.Length && batch.Count < BATCH_SIZE; i++)
            {
                string fullPath = files[i];
                string fileName = Path.GetFileName(fullPath);

                if (fileName == DATABASE_FILE_NAME)
                {
                    continue;
                }

                string key = string.Empty;
                if (fileName.StartsWith("1."))
                {
                    key = fileName[2..];
                }

                if (string.IsNullOrEmpty(key))
                {
                    DeleteFile(fullPath);
                    continue;
                }

                batch[key] = fullPath;
            }

            Dictionary<string, long>? result = _database.BatchQuery(batch.Keys);
            if (result is null)
            {
                return;
            }

            foreach (KeyValuePair<string, long> pair in result)
            {
                if (pair.Value < 0)
                {
                    AddPendingFlushKey(pair.Key);
                }
                else
                {
                    lastUsedTimes.Add(new Tuple<string, long>(batch[pair.Key], pair.Value));
                }
            }
        }

        lastUsedTimes.Sort(new Comparison<Tuple<string, long>>((x, y) =>
        {
            return x.Item2.CompareTo(y.Item2);
        }));

        foreach (Tuple<string, long> item in lastUsedTimes)
        {
            if (sizeToRemove <= 0)
            {
                break;
            }

            DeleteFile(item.Item1);
        }
    }

    private string ToHashedKey(string key)
    {
        return key;
    }

    private static string GetDirtyFileName(string key)
    {
        return "0." + key;
    }

    private static string GetCleanFileName(string key)
    {
        return "1." + key;
    }

    private void AddPendingFlushKey(string key)
    {
        _flushLock.AcquireReaderLock(-1);
        try
        {
            _pendingFlushKeys[key] = DateTimeOffset.Now.ToUnixTimeSeconds();
            if (Interlocked.CompareExchange(ref _postFlushTask, 1, 0) == 1)
            {
                return;
            }
        }
        finally
        {
            _flushLock.ReleaseReaderLock();
        }

        CoroutineUtils.Run(async () =>
        {
            await Task.Delay(1000);
            IDictionary<string, long> pendingFlushKeys;
            _flushLock.AcquireWriterLock(-1);
            try
            {
                pendingFlushKeys = _pendingFlushKeys;
                _pendingFlushKeys = new();
                Interlocked.Exchange(ref _postFlushTask, 0);
            }
            finally
            {
                _flushLock.ReleaseWriterLock();
            }

            _database.BatchUpdate(pendingFlushKeys);
        });
    }

    private class CacheEntry
    {
        private readonly ReaderWriterLock _lock = new();
        private readonly LRUCache _cache;
        private readonly string _key;
        private Status _status;
        private int _readerCount = 0;

        public CacheEntry(LRUCache cache, string key)
        {
            _cache = cache;
            _key = key;
            _status = Status.Empty;
        }

        public LRUInputStream? StartWrite()
        {
            _lock.AcquireWriterLock(-1);
            try
            {
                if (_status == Status.Dirty || _readerCount > 0)
                {
                    Logger.F(TAG, "Other read/write operation in progress");
                    return null;
                }

                _status = Status.Dirty;
            }
            finally
            {
                _lock.ReleaseWriterLock();
            }

            string filePath = Path.Combine(_cache._directoryPath, GetDirtyFileName(_key));
            Stream? stream = null;
            try
            {
                stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            }
            catch (Exception e)
            {
                Logger.F(TAG, nameof(StartWrite), e);
            }

            if (stream == null)
            {
                SwitchToEmptyState();
                return null;
            }

            return new LRUInputStream(this, stream);
        }

        public void EndWrite()
        {
            string oldPath = Path.Combine(_cache._directoryPath, GetDirtyFileName(_key));
            string newPath = Path.Combine(_cache._directoryPath, GetCleanFileName(_key));
            try
            {
                File.Move(oldPath, newPath, overwrite: true);
            }
            catch (Exception e)
            {
                Logger.F(TAG, nameof(EndWrite), e);
                SwitchToEmptyState();
                return;
            }

            _lock.AcquireWriterLock(-1);
            try
            {
                Logger.Assert(_status == Status.Dirty, "77504AC355E8488C");
                _status = Status.Clean;
            }
            finally
            {
                _lock.ReleaseWriterLock();
            }
        }

        public LRUOutputStream? StartRead()
        {
            _lock.AcquireWriterLock(-1);
            try
            {
                if (_status == Status.Dirty)
                {
                    return null;
                }

                string filePath = Path.Combine(_cache._directoryPath, GetCleanFileName(_key));
                Stream? stream = null;
                try
                {
                    stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                }
                catch (FileNotFoundException)
                {
                }
                catch (Exception e)
                {
                    Logger.F(TAG, "StartRead", e);
                }

                if (stream == null)
                {
                    return null;
                }

                _status = Status.Clean;
                _readerCount++;
                return new LRUOutputStream(this, stream);
            }
            finally
            {
                _lock.ReleaseWriterLock();
            }
        }

        public void EndRead()
        {
            _lock.AcquireWriterLock(-1);
            try
            {
                Logger.Assert(_status == Status.Clean, "302BF5B1FB061FC9");
                Logger.Assert(_readerCount > 0, "40B639B2784A378E");

                if (_readerCount <= 0)
                {
                    return;
                }

                _readerCount--;
            }
            finally
            {
                _lock.ReleaseWriterLock();
            }
        }

        private void SwitchToEmptyState()
        {
            _lock.AcquireWriterLock(-1);
            try
            {
                _status = Status.Empty;
            }
            finally
            {
                _lock.ReleaseWriterLock();
            }
        }

        private enum Status
        {
            Empty,
            Clean,
            Dirty,
        }
    }

    private class LRUInputStream(CacheEntry entry, Stream stream) : LRUCacheStream(stream)
    {
        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                Inner.Dispose();
                entry.EndWrite();
            }

            _disposed = true;
            base.Dispose(disposing);
        }
    }

    private class LRUOutputStream(CacheEntry entry, Stream stream) : LRUCacheStream(stream)
    {
        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                Inner.Dispose();
                entry.EndRead();
            }

            _disposed = true;
            base.Dispose(disposing);
        }
    }
}
