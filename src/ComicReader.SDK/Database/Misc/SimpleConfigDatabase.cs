// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.Caching;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Storage;

using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace ComicReader.SDK.Database.Misc;

internal class SimpleConfigDatabase
{
    private const string TAG = nameof(SimpleConfigDatabase);

    private static readonly SimpleConfigDatabase sInstance = new(StorageLocation.LocalFolderPath, "database_common");

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

        ILRUOutputStream? stream = lruCache.Get(key);
        if (stream is null)
        {
            return null;
        }

        using DataReader reader = new(stream);
        string value;
        try
        {
            uint bytesLoaded = reader.LoadAsync((uint)stream.Size).AsTask().Result;
            if (bytesLoaded == 0)
            {
                Logger.AssertNotReachHere("7EFEE0FD9C031188");
                return null;
            }

            IBuffer buffer = reader.ReadBuffer(bytesLoaded);
            value = CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, buffer);
        }
        catch (Exception ex)
        {
            Logger.F(TAG, nameof(TryGetConfig), ex);
            return null;
        }

        return value;
    }

    public void TryPutConfig(string key, string value)
    {
        LRUCache? lruCache = GetLRUCache();
        if (lruCache is null)
        {
            return;
        }

        IBuffer buffer = CryptographicBuffer.ConvertStringToBinary(value, BinaryStringEncoding.Utf8);
        using ILRUInputStream? stream = lruCache.Put(key);
        if (stream is null)
        {
            Logger.F(TAG, "Failed to acquire input stream.");
            return;
        }

        try
        {
            stream.WriteAsync(buffer).Wait();
        }
        catch (Exception ex)
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
