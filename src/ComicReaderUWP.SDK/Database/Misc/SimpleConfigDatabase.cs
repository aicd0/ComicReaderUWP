// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using ComicReaderUWP.SDK.Common.Caching;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Storage;

namespace ComicReaderUWP.SDK.Database.Misc;

internal class SimpleConfigDatabase
{
    private const string TAG = nameof(SimpleConfigDatabase);

    private static readonly SimpleConfigDatabase sInstance = new(StorageLocation.LocalFolderPath, "configs");

    public static SimpleConfigDatabase Instance => sInstance;

    private readonly object _lock = new();
    private readonly string _directoryPath;
    private volatile LRUCache? _lruCache = null;

    private SimpleConfigDatabase(string directoryPath, string name)
    {
        _directoryPath = Path.Combine(directoryPath, name);
    }

    public string? TryGetConfig(string key)
    {
        LRUCache? lruCache = GetLRUCache();
        if (lruCache is null)
        {
            return null;
        }

        using LRUCacheStream? stream = lruCache.Get(key);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);
        try
        {
            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            Logger.F(TAG, nameof(TryGetConfig), ex);
            return null;
        }
    }

    public void TryPutConfig(string key, string value)
    {
        LRUCache? lruCache = GetLRUCache();
        if (lruCache is null)
        {
            return;
        }

        using LRUCacheStream? stream = lruCache.Put(key);
        if (stream is null)
        {
            Logger.F(TAG, "Failed to acquire input stream");
            return;
        }

        using var writer = new StreamWriter(
            stream,
            Encoding.UTF8,
            bufferSize: 1024,
            leaveOpen: true);
        try
        {
            writer.Write(value);
        }
        catch (IOException ex)
        {
            Logger.F(TAG, nameof(TryPutConfig), ex);
            return;
        }
    }

    private LRUCache? GetLRUCache()
    {
        {
            LRUCache? cache = _lruCache;
            if (cache is not null)
            {
                return cache;
            }
        }

        lock (_lock)
        {
            LRUCache? cache = _lruCache;
            if (cache is not null)
            {
                return cache;
            }

            try
            {
                Directory.CreateDirectory(_directoryPath);
            }
            catch (Exception ex)
            {
                Logger.F(TAG, nameof(GetLRUCache), ex);
                return null;
            }

            cache = new(_directoryPath, 0);
            _lruCache = cache;
            return cache;
        }
    }
}
