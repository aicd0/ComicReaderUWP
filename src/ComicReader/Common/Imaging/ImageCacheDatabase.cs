// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ComicReader.Common.Legacy;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Storage;

using Microsoft.Data.Sqlite;

using Windows.Storage;

namespace ComicReader.Common.Imaging;

internal static class ImageCacheDatabase
{
    public const string TAG = "ImageCacheDatabase";
    public const string CACHE_TABLE = "cache";
    public const string CACHE_TABLE_FIELD_KEY = "key";
    public const string CACHE_TABLE_FIELD_SIGNATURE = "signature";
    public const string CACHE_TABLE_FIELD_WIDTH = "width";
    public const string CACHE_TABLE_FIELD_HEIGHT = "height";
    public const string CACHE_TABLE_FIELD_ENTRIES = "entries";

    private const string DATABASE_FILE_NAME = "image_cache.db";

    private static readonly object _connectionLock = new();
    private static SqliteConnection _connection;

    private static readonly ReaderWriterLock _recordCacheLock = new();
    private static readonly Dictionary<string, CacheRecord> _recordCache = new();

    private static StorageFolder _databaseFolder;
    private static StorageFolder DatabaseFolder
    {
        get
        {
            if (_databaseFolder == null)
            {
                string folderPath = StorageLocation.LocalCacheFolderPath;
                StorageFolder databaseFolder = Storage.TryGetFolder(folderPath).Result;
                if (databaseFolder == null)
                {
                    Logger.AssertNotReachHere("C337BD5DF4FAE670");
                    return null;
                }
                _databaseFolder = databaseFolder.CreateFolderAsync("database", CreationCollisionOption.OpenIfExists).Get();
            }
            return _databaseFolder;
        }
    }

    public static CacheRecord GetCacheRecord(IImageSource source)
    {
        CacheRecord record = GetCacheRecord(source.GetUri());

        if (record == null)
        {
            return null;
        }

        int sourceSignature = source.GetContentSignature();

        if (sourceSignature != 0 && record.Signature != sourceSignature)
        {
            return null;
        }

        return record;
    }

    private static CacheRecord GetCacheRecord(string key)
    {
        if (key == null || key.Length == 0)
        {
            return null;
        }

        string cacheKey = ToHashedKey(key);

        _recordCacheLock.AcquireReaderLock(-1);
        try
        {
            if (_recordCache.TryGetValue(cacheKey, out CacheRecord record))
            {
                return record;
            }
        }
        finally
        {
            _recordCacheLock.ReleaseReaderLock();
        }

        lock (_connectionLock)
        {
            _recordCacheLock.AcquireReaderLock(-1);
            try
            {
                if (_recordCache.TryGetValue(cacheKey, out CacheRecord record))
                {
                    return record;
                }
            }
            finally
            {
                _recordCacheLock.ReleaseReaderLock();
            }

            SqliteConnection connection = GetConnection();
            if (connection == null)
            {
                return null;
            }

            List<CacheRecord> records = new();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = $"SELECT " +
                    $"{CACHE_TABLE_FIELD_SIGNATURE}" +
                    $",{CACHE_TABLE_FIELD_WIDTH}" +
                    $",{CACHE_TABLE_FIELD_HEIGHT}" +
                    $",{CACHE_TABLE_FIELD_ENTRIES}" +
                    $" FROM {CACHE_TABLE} WHERE {CACHE_TABLE_FIELD_KEY}=@key";
                command.Parameters.AddWithValue("@key", cacheKey);

                using SqliteDataReader query = command.ExecuteReader();
                while (query.Read())
                {
                    int signature = query.GetInt32(0);
                    int width = query.GetInt32(1);
                    int height = query.GetInt32(2);
                    string entries = query.GetString(3);
                    CacheRecord record = new(key, signature, width, height, entries);
                    records.Add(record);
                }
            }
            Logger.Assert(records.Count <= 1, "9C0107871C1B6CB1");

            if (records.Count == 0)
            {
                return null;
            }
            CacheRecord firstRecord = records[0];

            _recordCacheLock.AcquireWriterLock(-1);
            try
            {
                if (_recordCache.TryGetValue(cacheKey, out CacheRecord record))
                {
                    return record;
                }
                _recordCache[cacheKey] = firstRecord;
            }
            finally
            {
                _recordCacheLock.ReleaseWriterLock();
            }

            return firstRecord;
        }
    }

    private static string ToHashedKey(string key)
    {
        return string.Concat(SHA256.HashData(Encoding.UTF8.GetBytes(key))
            .Select(item => item.ToString("x2"))
            .Take(16));
    }

    private static SqliteConnection GetConnection()
    {
        if (_connection != null)
        {
            return _connection;
        }

        lock (_connectionLock)
        {
            try
            {
                _connection = CreateConnection(false).Result;
            }
            catch (Exception ex)
            {
                Logger.F(TAG, "GetConnection", ex);
            }

            if (_connection == null)
            {
                try
                {
                    _connection = CreateConnection(true).Result;
                }
                catch (Exception ex)
                {
                    Logger.F(TAG, "GetConnection", ex);
                }
            }
        }
        return _connection;
    }

    private static async Task<SqliteConnection> CreateConnection(bool clear)
    {
        StorageFolder databaseFolder = DatabaseFolder;
        if (databaseFolder == null)
        {
            Logger.AssertNotReachHere("DD5103C3E794B6A6");
            return null;
        }
        StorageFile databaseFile = await databaseFolder.CreateFileAsync(DATABASE_FILE_NAME,
            clear ? CreationCollisionOption.ReplaceExisting : CreationCollisionOption.OpenIfExists);
        var connection = new SqliteConnection($"Filename={databaseFile.Path}");
        connection.Open();

        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE IF NOT EXISTS " + CACHE_TABLE + " (" +
                CACHE_TABLE_FIELD_KEY + " TEXT PRIMARY KEY," +
                CACHE_TABLE_FIELD_SIGNATURE + " INTEGER NOT NULL," +
                CACHE_TABLE_FIELD_WIDTH + " INTEGER NOT NULL," +
                CACHE_TABLE_FIELD_HEIGHT + " INTEGER NOT NULL," +
                CACHE_TABLE_FIELD_ENTRIES + " TEXT)";
            await command.ExecuteNonQueryAsync();
        }

        return connection;
    }

    public class CacheRecord
    {
        private readonly ReaderWriterLock _lock = new();
        private readonly string _key;
        private int _signature;
        private int _width;
        private int _height;
        private readonly Dictionary<string, string> _entries;
        private bool _updated;

        public int Signature => _signature;
        public int Width => _width;
        public int Height => _height;

        public CacheRecord(string key, int signature, int width, int height)
        {
            _key = key;
            _signature = signature;
            _width = width;
            _height = height;
            _entries = [];
            _updated = true;
        }

        public CacheRecord(string key, int signature, int width, int height, string cacheEntriesJson)
        {
            _key = key;
            _signature = signature;
            _width = width;
            _height = height;

            try
            {
                _entries = JsonSerializer.Deserialize<Dictionary<string, string>>(cacheEntriesJson);
            }
            catch (Exception e)
            {
                Logger.E(TAG, "CacheRecord", e);
                _entries = [];
            }

            _updated = false;
        }

        public void Save()
        {
            _lock.AcquireReaderLock(-1);
            try
            {
                if (!_updated)
                {
                    return;
                }
            }
            finally
            {
                _lock.ReleaseReaderLock();
            }

            string cacheKey = ToHashedKey(_key);

            lock (_connectionLock)
            {
                string entries;
                int signature, width, height;

                _lock.AcquireWriterLock(-1);
                try
                {
                    if (!_updated)
                    {
                        return;
                    }

                    _recordCacheLock.AcquireWriterLock(-1);
                    try
                    {
                        if (_recordCache.TryGetValue(cacheKey, out CacheRecord record))
                        {
                            foreach (KeyValuePair<string, string> entry in record._entries)
                            {
                                _entries.TryAdd(entry.Key, entry.Value);
                            }
                        }
                        _recordCache[cacheKey] = this;
                    }
                    finally
                    {
                        _recordCacheLock.ReleaseWriterLock();
                    }

                    entries = JsonSerializer.Serialize(_entries);
                    signature = _signature;
                    width = _width;
                    height = _height;
                    _updated = false;
                }
                finally
                {
                    _lock.ReleaseWriterLock();
                }

                bool success = false;
                try
                {
                    SqliteConnection connection = GetConnection();
                    if (connection == null)
                    {
                        Logger.F(TAG, $"Failed to save cache {_key}, unable to create database connection.");
                        return;
                    }

                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.CommandText = $"INSERT OR REPLACE INTO {CACHE_TABLE}({CACHE_TABLE_FIELD_KEY}" +
                            $",{CACHE_TABLE_FIELD_SIGNATURE}" +
                            $",{CACHE_TABLE_FIELD_WIDTH}" +
                            $",{CACHE_TABLE_FIELD_HEIGHT}" +
                            $",{CACHE_TABLE_FIELD_ENTRIES}" +
                            $") VALUES(@key,@signature,@width,@height,@entries)";
                        command.Parameters.AddWithValue("@key", cacheKey);
                        command.Parameters.AddWithValue("@signature", signature);
                        command.Parameters.AddWithValue("@width", width);
                        command.Parameters.AddWithValue("@height", height);
                        command.Parameters.AddWithValue("@entries", entries);
                        command.ExecuteNonQuery();
                    }

                    success = true;
                }
                finally
                {
                    if (!success)
                    {
                        _lock.AcquireWriterLock(-1);
                        try
                        {
                            _updated = true;
                        }
                        finally
                        {
                            _lock.ReleaseWriterLock();
                        }
                    }
                }
            }
        }

        public string GetEntry(string key)
        {
            _lock.AcquireReaderLock(-1);
            try
            {
                if (_entries.TryGetValue(key, out string entry))
                {
                    return entry;
                }
            }
            finally
            {
                _lock.ReleaseReaderLock();
            }

            return "";
        }

        public void PutEntry(string key, string entry)
        {
            _lock.AcquireWriterLock(-1);
            try
            {
                _entries[key] = entry;
                _updated = true;
            }
            finally
            {
                _lock.ReleaseWriterLock();
            }
        }

        public void UpdateMeta(int signature, int width, int height)
        {
            _lock.AcquireReaderLock(-1);
            try
            {
                if ((signature != 0 && signature != _signature) || width != _width || height != _height)
                {
                    LockCookie cookie = _lock.UpgradeToWriterLock(-1);
                    try
                    {
                        if (signature != 0)
                        {
                            _signature = signature;
                        }
                        _width = width;
                        _height = height;
                        _updated = true;
                    }
                    finally
                    {
                        _lock.DowngradeFromWriterLock(ref cookie);
                    }
                }
            }
            finally
            {
                _lock.ReleaseReaderLock();
            }
        }
    }
}
