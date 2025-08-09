// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Concurrent;

using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Utils;

using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.SDK.Common.Caching;

public class LRUCache(string directoryPath, long maxSize)
{
    private const string TAG = nameof(LRUCache);
    private const string DATABASE_FILE_NAME = "info.db";
    private const int BATCH_SIZE = 1000;

    private readonly string _directoryPath = directoryPath;
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = [];

    private readonly long _maxSize = maxSize;
    private readonly LRUCacheDatabase _database = new(Path.Combine(directoryPath, DATABASE_FILE_NAME));
    private readonly ReaderWriterLock _flushLock = new();
    private volatile StorageFolder _folder = null;
    private volatile ConcurrentDictionary<string, long> _pendingFlushKeys = [];
    private int _postFlushTask = 0;

    public void Clear()
    {
        _database.Clear();
    }

    public ILRUInputStream Put(string key)
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));

        string hashedKey = ToHashedKey(key);
        CacheEntry entry = _entries.GetOrAdd(hashedKey, key => new CacheEntry(this, key));
        ILRUInputStream stream = entry.StartWrite();
        if (stream != null)
        {
            AddPendingFlushKey(key);
        }

        return stream;
    }

    public ILRUOutputStream Get(string key)
    {
        ArgumentNullException.ThrowIfNull(key, nameof(key));

        string hashedKey = ToHashedKey(key);
        CacheEntry entry = _entries.GetOrAdd(hashedKey, key => new CacheEntry(this, key));
        ILRUOutputStream stream = entry.StartRead();
        if (stream != null)
        {
            AddPendingFlushKey(key);
        }

        return stream;
    }

    public void Cleanup()
    {
        var directory = new DirectoryInfo(_directoryPath);
        long sizeToRemove = FileUtils.GetApproximateDirectorySize(directory) - _maxSize;
        if (sizeToRemove <= 0)
        {
            return;
        }

        IReadOnlyList<StorageFile> files;
        try
        {
            files = GetFolder().GetFilesAsync().AsTask().Result;
        }
        catch (Exception ex)
        {
            Logger.F(TAG, "Clean", ex);
            return;
        }

        List<Tuple<StorageFile, long>> lastUsedTimes = [];
        for (int i = 0; i < files.Count;)
        {
            Dictionary<string, StorageFile> batch = [];
            for (; i < files.Count && batch.Count < BATCH_SIZE; i++)
            {
                StorageFile file = files[i];
                string fileName = file.Name;

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
                    try
                    {
                        ulong fileSize = file.GetBasicPropertiesAsync().AsTask().Result.Size;
                        file.DeleteAsync().Wait();
                        sizeToRemove -= (long)fileSize;
                    }
                    catch (Exception ex)
                    {
                        Logger.F(TAG, "Clean", ex);
                    }

                    continue;
                }

                batch[key] = file;
            }

            Dictionary<string, long> result = _database.BatchQuery(batch.Keys);
            foreach (KeyValuePair<string, long> pair in result)
            {
                if (pair.Value < 0)
                {
                    AddPendingFlushKey(pair.Key);
                }
                else
                {
                    lastUsedTimes.Add(new Tuple<StorageFile, long>(batch[pair.Key], pair.Value));
                }
            }
        }

        lastUsedTimes.Sort(new Comparison<Tuple<StorageFile, long>>(
            delegate (Tuple<StorageFile, long> x, Tuple<StorageFile, long> y) { return x.Item2.CompareTo(y.Item2); }));
        foreach (Tuple<StorageFile, long> item in lastUsedTimes)
        {
            if (sizeToRemove <= 0)
            {
                break;
            }

            StorageFile file = item.Item1;
            try
            {
                ulong fileSize = file.GetBasicPropertiesAsync().AsTask().Result.Size;
                file.DeleteAsync().Wait();
                sizeToRemove -= (long)fileSize;
            }
            catch (Exception ex)
            {
                Logger.F(TAG, "Clean", ex);
            }
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

        Task.Delay(1000).ContinueWith(delegate
        {
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

    private StorageFolder GetFolder()
    {
        StorageFolder folder = _folder;
        if (folder != null)
        {
            return folder;
        }
        folder = StorageFolder.GetFolderFromPathAsync(_directoryPath).AsTask().Result;
        _folder = folder;
        return folder;
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

        public ILRUInputStream StartWrite()
        {
            _lock.AcquireWriterLock(-1);
            try
            {
                if (_status == Status.Dirty || _readerCount > 0)
                {
                    return null;
                }
                _status = Status.Dirty;
            }
            finally
            {
                _lock.ReleaseWriterLock();
            }

            string dirtyFileName = GetDirtyFileName(_key);
            StorageFile file = null;
            try
            {
                file = _cache.GetFolder().CreateFileAsync(dirtyFileName, CreationCollisionOption.ReplaceExisting).AsTask().Result;
            }
            catch (Exception e)
            {
                Logger.F(TAG, nameof(StartWrite), e);
            }
            if (file == null)
            {
                SwitchToEmptyState();
                return null;
            }

            IRandomAccessStream stream = null;
            try
            {
                stream = file.OpenAsync(FileAccessMode.ReadWrite).AsTask().Result;
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
            string dirtyFileName = GetDirtyFileName(_key);
            StorageFile file = null;
            try
            {
                file = _cache.GetFolder().GetFileAsync(dirtyFileName).AsTask().Result;
            }
            catch (Exception e)
            {
                Logger.F(TAG, nameof(EndWrite), e);
            }
            if (file == null)
            {
                SwitchToEmptyState();
                return;
            }

            string cleanFileName = GetCleanFileName(_key);
            try
            {
                file.RenameAsync(cleanFileName, NameCollisionOption.ReplaceExisting).AsTask().Wait();
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

        public ILRUOutputStream StartRead()
        {
            _lock.AcquireWriterLock(-1);
            try
            {
                if (_status == Status.Dirty)
                {
                    return null;
                }

                string cleanFileName = GetCleanFileName(_key);
                StorageFile file = null;
                try
                {
                    file = _cache.GetFolder().GetFileAsync(cleanFileName).AsTask().Result;
                }
                catch (Exception e)
                {
                    Logger.E(TAG, "StartRead", e);
                }
                if (file == null)
                {
                    return null;
                }

                IRandomAccessStream stream = null;
                try
                {
                    stream = file.OpenAsync(FileAccessMode.Read).AsTask().Result;
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

    private class LRUInputStream(CacheEntry entry, IRandomAccessStream stream) : ILRUInputStream
    {
        public void Dispose()
        {
            stream.Dispose();
            entry.EndWrite();
        }

        public async Task WriteAsync(IBuffer buffer)
        {
            await stream.WriteAsync(buffer);
        }
    }

    private class LRUOutputStream(CacheEntry entry, IRandomAccessStream stream) : ILRUOutputStream
    {
        public bool CanRead => stream.CanRead;

        public bool CanWrite => stream.CanWrite;

        public ulong Position => stream.Position;

        public ulong Size
        {
            get => stream.Size;
            set => stream.Size = value;
        }

        public IRandomAccessStream CloneStream()
        {
            return stream.CloneStream();
        }

        public void Dispose()
        {
            entry.EndRead();
            stream.Dispose();
        }

        public IAsyncOperation<bool> FlushAsync()
        {
            return stream.FlushAsync();
        }

        public IInputStream GetInputStreamAt(ulong position)
        {
            return stream.GetInputStreamAt(position);
        }

        public IOutputStream GetOutputStreamAt(ulong position)
        {
            return stream.GetOutputStreamAt(position);
        }

        public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options)
        {
            return stream.ReadAsync(buffer, count, options);
        }

        public void Seek(ulong position)
        {
            stream.Seek(position);
        }

        public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer)
        {
            return stream.WriteAsync(buffer);
        }
    }
}
