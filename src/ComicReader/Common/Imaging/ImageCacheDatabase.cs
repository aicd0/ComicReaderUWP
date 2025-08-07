// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Storage;

using Microsoft.Data.Sqlite;

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

    private static readonly object _databaseLock = new();
    private static SqliteConnection? _connection;

    private static readonly ReaderWriterLock _recordCacheLock = new();
    private static readonly Dictionary<string, CacheRecord?> _recordCache = [];

    private static string DatabaseFolderPath => StorageLocation.LocalCacheFolderPath;

    public static void Clear()
    {
        lock (_databaseLock)
        {
            _recordCacheLock.AcquireWriterLock(-1);
            try
            {
                _recordCache.Clear();
            }
            finally
            {
                _recordCacheLock.ReleaseWriterLock();
            }

            SqliteConnection? connection = GetConnectionNoLock();
            if (connection is not null)
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "DELETE FROM " + CACHE_TABLE;
                command.ExecuteNonQuery();
            }
        }
    }

    public static CacheRecord? GetCacheRecord(IImageSource source)
    {
        CacheRecord? record = GetCacheRecord(source.GetUri());
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

    private static CacheRecord? GetCacheRecord(string key)
    {
        if (key == null || key.Length == 0)
        {
            return null;
        }

        string hashedKey = ToHashedKey(key);

        _recordCacheLock.AcquireReaderLock(-1);
        try
        {
            if (_recordCache.TryGetValue(hashedKey, out CacheRecord? record))
            {
                return record;
            }
        }
        finally
        {
            _recordCacheLock.ReleaseReaderLock();
        }

        lock (_databaseLock)
        {
            if (_recordCache.TryGetValue(hashedKey, out CacheRecord? targetRecord))
            {
                return targetRecord;
            }

            SqliteConnection? connection = GetConnectionNoLock();
            if (connection == null)
            {
                return null;
            }

            List<CacheRecord> records = [];
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = $"SELECT " +
                    $"{CACHE_TABLE_FIELD_SIGNATURE}" +
                    $",{CACHE_TABLE_FIELD_WIDTH}" +
                    $",{CACHE_TABLE_FIELD_HEIGHT}" +
                    $",{CACHE_TABLE_FIELD_ENTRIES}" +
                    $" FROM {CACHE_TABLE} WHERE {CACHE_TABLE_FIELD_KEY}=@key";
                command.Parameters.AddWithValue("@key", hashedKey);

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
                targetRecord = null;
            }
            else
            {
                targetRecord = records[0];
            }

            _recordCacheLock.AcquireWriterLock(-1);
            try
            {
                _recordCache[hashedKey] = targetRecord;
            }
            finally
            {
                _recordCacheLock.ReleaseWriterLock();
            }

            return targetRecord;
        }
    }

    private static string ToHashedKey(string key)
    {
        return string.Concat(SHA256.HashData(Encoding.UTF8.GetBytes(key))
            .Select(item => item.ToString("x2"))
            .Take(16));
    }

    private static SqliteConnection? GetConnectionNoLock()
    {
        if (_connection != null)
        {
            return _connection;
        }

        try
        {
            _connection = CreateConnection(false).Result;
        }
        catch (Exception ex)
        {
            Logger.F(TAG, "GetConnection", ex);
        }

        if (_connection is null)
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

        return _connection;
    }

    private static async Task<SqliteConnection?> CreateConnection(bool clear)
    {
        string databaseFolderPath = DatabaseFolderPath;
        if (!Directory.Exists(databaseFolderPath))
        {
            try
            {
                Directory.CreateDirectory(databaseFolderPath);
            }
            catch (Exception ex)
            {
                Logger.F(TAG, "CreateConnection", ex);
                return null;
            }
        }

        string databaseFilePath = Path.Combine(databaseFolderPath, DATABASE_FILE_NAME);
        if (clear || !File.Exists(databaseFilePath))
        {
            try
            {
                File.Create(databaseFilePath).Dispose();
            }
            catch (Exception ex)
            {
                Logger.F(TAG, "CreateConnection", ex);
                return null;
            }
        }

        var connection = new SqliteConnection($"Filename={databaseFilePath}");
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
        private readonly string _key;
        private int _updated;
        private int _signature;
        private int _width;
        private int _height;
        private readonly ConcurrentDictionary<string, string> _entries;

        public int Signature => _signature;
        public int Width => _width;
        public int Height => _height;

        public CacheRecord(string key, int signature, int width, int height)
        {
            _key = key;
            _updated = 1;
            _signature = signature;
            _width = width;
            _height = height;
            _entries = [];
        }

        public CacheRecord(string key, int signature, int width, int height, string cacheEntriesJson)
        {
            _key = key;
            _updated = 0;
            _signature = signature;
            _width = width;
            _height = height;

            try
            {
                _entries = new(JsonSerializer.Deserialize<Dictionary<string, string>>(cacheEntriesJson) ?? []);
            }
            catch (Exception e)
            {
                Logger.E(TAG, "CacheRecord", e);
                _entries = [];
            }
        }

        public void Save()
        {
            if (Interlocked.CompareExchange(ref _updated, 0, 1) == 0)
            {
                return;
            }

            string hashedKey = ToHashedKey(_key);

            lock (_databaseLock)
            {
                if (_recordCache.TryGetValue(hashedKey, out CacheRecord? record))
                {
                    if (record != null)
                    {
                        foreach (KeyValuePair<string, string> entry in record._entries)
                        {
                            _entries.TryAdd(entry.Key, entry.Value);
                        }
                    }
                }

                _recordCacheLock.AcquireWriterLock(-1);
                try
                {
                    _recordCache[hashedKey] = this;
                }
                finally
                {
                    _recordCacheLock.ReleaseWriterLock();
                }

                string entries = JsonSerializer.Serialize(_entries);
                int signature = _signature;
                int width = _width;
                int height = _height;

                SqliteConnection? connection = GetConnectionNoLock();
                if (connection is null)
                {
                    Logger.F(TAG, $"Failed to save cache {_key}, unable to create database connection.");
                    Interlocked.Exchange(ref _updated, 1);
                    return;
                }

                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = $"INSERT OR REPLACE INTO {CACHE_TABLE}({CACHE_TABLE_FIELD_KEY}" +
                    $",{CACHE_TABLE_FIELD_SIGNATURE}" +
                    $",{CACHE_TABLE_FIELD_WIDTH}" +
                    $",{CACHE_TABLE_FIELD_HEIGHT}" +
                    $",{CACHE_TABLE_FIELD_ENTRIES}" +
                    $") VALUES(@key,@signature,@width,@height,@entries)";
                command.Parameters.AddWithValue("@key", hashedKey);
                command.Parameters.AddWithValue("@signature", signature);
                command.Parameters.AddWithValue("@width", width);
                command.Parameters.AddWithValue("@height", height);
                command.Parameters.AddWithValue("@entries", entries);
                command.ExecuteNonQuery();
            }
        }

        public string GetEntry(string key)
        {
            if (_entries.TryGetValue(key, out string? entry))
            {
                return entry;
            }

            return string.Empty;
        }

        public void PutEntry(string key, string entry)
        {
            _entries[key] = entry;
            Interlocked.Exchange(ref _updated, 1);
        }

        public void UpdateMeta(int signature, int width, int height)
        {
            bool updated = false;

            if (signature != 0 && signature != _signature)
            {
                _signature = signature;
                updated = true;
            }

            if (width != _width)
            {
                _width = width;
                updated = true;
            }

            if (height != _height)
            {
                _height = height;
                updated = true;
            }

            if (updated)
            {
                Interlocked.Exchange(ref _updated, 1);
            }
        }
    }
}
