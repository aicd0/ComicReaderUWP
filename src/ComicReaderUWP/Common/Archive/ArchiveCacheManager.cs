// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

using ComicReaderUWP.Core.Common.Caching;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Storage;
using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Core.Common.Utils;

namespace ComicReaderUWP.Common.Archive;

internal static class ArchiveCacheManager
{
    private const string TAG = nameof(ArchiveCacheManager);
    private const string CACHE_FOLDER_NAME = "ArchiveCache";
    private const long MAX_CACHE_SIZE = 256 * 1024 * 1024;

    private static readonly Lock sLock = new();
    private static volatile LRUCache? sCache;
    private static readonly ConcurrentDictionary<string, Lock> sCacheKeyLocks = new();
    private static readonly ConcurrentDictionary<string, bool> sFailedKeys = new();

    private static string CacheDirectoryPath => Path.Combine(StorageLocation.LocalCacheFolderPath, CACHE_FOLDER_NAME);

    public static Stream? GetOrCreate(string basePath, string subPath, SharpCompress.Archives.IArchive archive)
    {
        string? key = ComputeKey(basePath, subPath);
        if (key is null || sFailedKeys.ContainsKey(key))
        {
            return null;
        }

        LRUCache? cache = GetCache();
        if (cache is null)
        {
            return null;
        }

        Lock keyLock = sCacheKeyLocks.GetOrAdd(key, _ => new());
        lock (keyLock)
        {
            if (sFailedKeys.ContainsKey(key))
            {
                return null;
            }

            LRUCacheStream? writeStream = cache.Put(key);
            if (writeStream is null)
            {
                return null;
            }

            bool success;
            try
            {
                success = RemuxSolidArchiveToZip(archive, writeStream);
            }
            catch (Exception ex)
            {
                Logger.F(TAG, nameof(GetOrCreate), ex);
                success = false;
            }
            finally
            {
                try
                {
                    writeStream.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.F(TAG, nameof(GetOrCreate), ex);
                }
            }

            if (!success)
            {
                sFailedKeys[key] = true;
                return null;
            }

            try
            {
                return cache.Get(key);
            }
            catch (Exception ex)
            {
                Logger.F(TAG, nameof(GetOrCreate), ex);
                return null;
            }
        }
    }

    public static Stream? Get(string basePath, string subPath)
    {
        string? key = ComputeKey(basePath, subPath);
        if (key is null || sFailedKeys.ContainsKey(key))
        {
            return null;
        }

        LRUCache? cache = GetCache();
        if (cache is null)
        {
            return null;
        }

        return cache.Get(key);
    }

    public static void Clear()
    {
        sCacheKeyLocks.Clear();
        sFailedKeys.Clear();

        LRUCache? cache;
        lock (sLock)
        {
            cache = sCache;
            sCache = null;
        }

        cache?.Clear();
    }

    public static SharpCompress.Readers.ReaderOptions CreateCachedArchiveReaderOptions()
    {
        return new()
        {
            ArchiveEncoding = new SharpCompress.Common.ArchiveEncoding()
            {
                Default = Encoding.UTF8,
            },
            ExtensionHint = ".zip",
        };
    }

    private static bool RemuxSolidArchiveToZip(SharpCompress.Archives.IArchive archive, Stream output)
    {
        var writerOptions = new SharpCompress.Writers.Zip.ZipWriterOptions(SharpCompress.Common.CompressionType.None)
        {
            LeaveStreamOpen = true,
            ArchiveEncoding = new SharpCompress.Common.ArchiveEncoding()
            {
                Default = Encoding.UTF8,
            },
        };

        try
        {
            using SharpCompress.Writers.IWriter writer = SharpCompress.Writers.WriterFactory.OpenWriter(output, SharpCompress.Common.ArchiveType.Zip, writerOptions);
            using SharpCompress.Readers.IReader reader = archive.ExtractAllEntries();
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = reader.MoveToNextEntry();
                }
                catch (Exception ex)
                {
                    Logger.F(TAG, nameof(RemuxSolidArchiveToZip), ex);
                    return false;
                }

                if (!hasNext)
                {
                    break;
                }

                if (reader.Entry.IsDirectory)
                {
                    continue;
                }

                string name = (reader.Entry.Key ?? string.Empty).Replace('\\', '/');
                if (name.Length == 0)
                {
                    continue;
                }

                using Stream entryStream = reader.OpenEntryStream();
                writer.Write(name, entryStream, null);
            }

            return true;
        }
        catch (SharpCompress.Common.CryptographicException ex)
        {
            Logger.E(TAG, nameof(RemuxSolidArchiveToZip), ex);
            return false;
        }
        catch (Exception ex)
        {
            Logger.F(TAG, nameof(RemuxSolidArchiveToZip), ex);
            return false;
        }
    }

    private static string? ComputeKey(string basePath, string subPath)
    {
        string signature = FileUtils.GetFileSignature(basePath);
        if (signature.Length == 0)
        {
            return null;
        }

        byte[] hash = HashUtils.GetXxHash64(signature + "\u0000" + subPath);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static LRUCache? GetCache()
    {
        LRUCache? cache = sCache;
        if (cache is not null)
        {
            return cache;
        }

        lock (sLock)
        {
            cache = sCache;
            if (cache is not null)
            {
                return cache;
            }

            try
            {
                Directory.CreateDirectory(CacheDirectoryPath);
            }
            catch (Exception ex)
            {
                Logger.F(TAG, nameof(GetCache), ex);
                return null;
            }

            cache = new LRUCache(CacheDirectoryPath);
            sCache = cache;
        }

        TaskDispatcher.LongRunningThreadPool.Submit(() =>
        {
            try
            {
                cache.Cleanup(MAX_CACHE_SIZE);
            }
            catch (Exception ex)
            {
                Logger.F(TAG, nameof(GetCache), ex);
            }
        });

        Logger.I(TAG, "Archive cache initialized");
        return cache;
    }
}
