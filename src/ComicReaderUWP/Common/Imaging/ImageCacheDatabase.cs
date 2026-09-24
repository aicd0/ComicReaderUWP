// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Utils;

using Microsoft.Data.Sqlite;

namespace ComicReaderUWP.Common.Imaging;

internal class ImageCacheDatabase(string databaseFilePath)
{
    private const string TAG = nameof(ImageCacheDatabase);
    private const string MAIN_TABLE = "Main";
    private const string COLUMN_KEY = "Key";
    private const string COLUMN_EXT = "Ext";
    private const string URI_CACHE_KEY_TABLE = "uri_cache_key";
    private const string COLUMN_URI = "uri";
    private const string COLUMN_CACHE_KEY = "cache_key";

    private readonly Lock _databaseLock = new();
    private SqliteConnection? _connection;
    private readonly ConcurrentDictionary<string, CacheRecord> _recordCache = [];
    private readonly ConcurrentDictionary<string, string> _uriCacheKeys = [];

    public void Clear()
    {
        lock (_databaseLock)
        {
            _recordCache.Clear();
            _uriCacheKeys.Clear();

            SqliteConnection? connection = GetConnectionNoLock();
            if (connection is not null)
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "DELETE FROM " + MAIN_TABLE + ";" + "DELETE FROM " + URI_CACHE_KEY_TABLE;
                command.ExecuteNonQuery();
            }
        }
    }

    public CacheRecord? GetCache(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        string hashedKey = ToHashedKey(key);
        return GetCache(hashedKey, key);
    }

    public CacheRecord GetOrCreateCache(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        string hashedKey = ToHashedKey(key);
        CacheRecord? record = GetCache(hashedKey, key);
        record ??= CacheRecord.CreateNew(this, key);
        return _recordCache.GetOrAdd(hashedKey, record);
    }

    private CacheRecord? GetCache(string hashedKey, string key)
    {
        {
            if (_recordCache.TryGetValue(hashedKey, out CacheRecord? record))
            {
                return record;
            }
        }

        lock (_databaseLock)
        {
            {
                if (_recordCache.TryGetValue(hashedKey, out CacheRecord? record))
                {
                    return record;
                }
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
                    $"{COLUMN_EXT}" +
                    $" FROM {MAIN_TABLE} WHERE {COLUMN_KEY}=@key";
                command.Parameters.AddWithValue("@key", hashedKey);
                using SqliteDataReader query = command.ExecuteReader();
                while (query.Read())
                {
                    string extJson = query.GetString(0);
                    Dictionary<string, string> ext = [];
                    try
                    {
                        ext = new(JsonSerializer.Deserialize<Dictionary<string, string>>(extJson) ?? []);
                    }
                    catch (Exception ex)
                    {
                        Logger.E(TAG, "CacheRecord", ex);
                        ext = [];
                    }

                    var record = CacheRecord.FromDatabase(this, key, ext);
                    records.Add(record);
                }
            }

            Logger.Assert(records.Count <= 1, "9C0107871C1B6CB1");
            if (records.Count == 0)
            {
                return null;
            }

            return records[0];
        }
    }

    public string? GetCacheKey(string? uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return null;
        }

        string hashedUri = ToHashedKey(uri);
        if (_uriCacheKeys.TryGetValue(hashedUri, out string? cacheKey))
        {
            return cacheKey;
        }

        lock (_databaseLock)
        {
            if (_uriCacheKeys.TryGetValue(hashedUri, out cacheKey))
            {
                return cacheKey;
            }

            SqliteConnection? connection = GetConnectionNoLock();
            if (connection is null)
            {
                return null;
            }

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {COLUMN_CACHE_KEY} FROM {URI_CACHE_KEY_TABLE} WHERE {COLUMN_URI}=@uri";
            command.Parameters.AddWithValue("@uri", hashedUri);
            using SqliteDataReader query = command.ExecuteReader();
            if (!query.Read())
            {
                return null;
            }

            cacheKey = query.GetString(0);
            _uriCacheKeys[hashedUri] = cacheKey;
            return cacheKey;
        }
    }

    public void SetCacheKey(string? uri, string? cacheKey)
    {
        if (string.IsNullOrEmpty(uri) || string.IsNullOrEmpty(cacheKey))
        {
            return;
        }

        string hashedUri = ToHashedKey(uri);
        if (_uriCacheKeys.TryGetValue(hashedUri, out string? existingCacheKey) && existingCacheKey == cacheKey)
        {
            return;
        }

        lock (_databaseLock)
        {
            SqliteConnection? connection = GetConnectionNoLock();
            if (connection is null)
            {
                return;
            }

            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = $"INSERT INTO {URI_CACHE_KEY_TABLE} ({COLUMN_URI},{COLUMN_CACHE_KEY}) VALUES (@uri,@cacheKey) " +
                    $"ON CONFLICT({COLUMN_URI}) DO UPDATE SET {COLUMN_CACHE_KEY}=@cacheKey";
                command.Parameters.AddWithValue("@uri", hashedUri);
                command.Parameters.AddWithValue("@cacheKey", cacheKey);
                command.ExecuteNonQuery();
            }

            _uriCacheKeys[hashedUri] = cacheKey;
        }
    }

    private static string ToHashedKey(string key)
    {
        byte[] hash = HashUtils.GetXxHash64(key);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private SqliteConnection? GetConnectionNoLock()
    {
        if (_connection != null)
        {
            return _connection;
        }

        try
        {
            _connection = CreateConnection(false);
        }
        catch (Exception ex)
        {
            Logger.F(TAG, "GetConnection", ex);
        }

        if (_connection is null)
        {
            try
            {
                _connection = CreateConnection(true);
            }
            catch (Exception ex)
            {
                Logger.F(TAG, "GetConnection", ex);
            }
        }

        return _connection;
    }

    private SqliteConnection? CreateConnection(bool clear)
    {
        string? databaseFolderPath = Path.GetDirectoryName(databaseFilePath);
        if (string.IsNullOrEmpty(databaseFolderPath))
        {
            Logger.F(TAG, "Database folder path is null or empty");
            return null;
        }

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
            command.CommandText = "CREATE TABLE IF NOT EXISTS " + MAIN_TABLE + " (" +
                COLUMN_KEY + " TEXT PRIMARY KEY," +
                COLUMN_EXT + " TEXT)";
            command.ExecuteNonQuery();
        }

        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE IF NOT EXISTS " + URI_CACHE_KEY_TABLE + " (" +
                COLUMN_URI + " TEXT PRIMARY KEY," +
                COLUMN_CACHE_KEY + " TEXT)";
            command.ExecuteNonQuery();
        }

        return connection;
    }

    public class CacheRecord
    {
        private const string INTERNAL_EXT_PREFIX = "_";
        private const string CACHE_ENTRY_PREFIX = "_CacheEntry_";

        public static CacheRecord FromDatabase(ImageCacheDatabase db, string key, IReadOnlyDictionary<string, string> ext)
        {
            CacheRecord record = new(db, key)
            {
                _updated = false,
            };

            foreach (KeyValuePair<string, string> entry in ext)
            {
                if (entry.Key.StartsWith(CACHE_ENTRY_PREFIX))
                {
                    string cacheKey = entry.Key[CACHE_ENTRY_PREFIX.Length..];
                    record._cacheEntries[cacheKey] = entry.Value;
                }
                else
                {
                    record._ext[entry.Key] = entry.Value;
                }
            }

            return record;
        }

        public static CacheRecord CreateNew(ImageCacheDatabase db, string key)
        {
            return new(db, key)
            {
                _updated = true,
            };
        }

        private readonly ImageCacheDatabase _database;
        private readonly string _key;
        private bool _updated;
        private readonly Dictionary<string, string> _cacheEntries = [];
        private readonly Dictionary<string, string> _ext = [];

        private readonly Lock _queueLock = new();
        private Task _queueTail = Task.CompletedTask;

        private CacheRecord(ImageCacheDatabase db, string key)
        {
            _database = db;
            _key = key;
        }

        public Task<T> Enqueue<T>(Func<Task<T>> func)
        {
            async Task RunAsync(Task previous, TaskCompletionSource<T> tcs)
            {
                try
                {
                    await previous;
                    T? result = await func();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }

            lock (_queueLock)
            {
                TaskCompletionSource<T> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _queueTail = RunAsync(_queueTail, tcs);
                return tcs.Task;
            }
        }

        public void Save()
        {
            if (!_updated)
            {
                return;
            }

            string hashedKey = ToHashedKey(_key);

            lock (_database._databaseLock)
            {
                if (!_updated)
                {
                    return;
                }

                _updated = false;

                // Write to database
                Dictionary<string, string> ext = new(_ext);
                foreach (KeyValuePair<string, string> entry in _cacheEntries)
                {
                    ext[CACHE_ENTRY_PREFIX + entry.Key] = entry.Value;
                }

                string extJson = JsonSerializer.Serialize(ext);

                SqliteConnection? connection = _database.GetConnectionNoLock();
                if (connection is null)
                {
                    Logger.F(TAG, $"Failed to save cache {_key}, unable to create database connection");
                    return;
                }

                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = $"INSERT OR REPLACE INTO {MAIN_TABLE}({COLUMN_KEY}" +
                    $",{COLUMN_EXT}" +
                    $") VALUES(@key,@ext)";
                command.Parameters.AddWithValue("@key", hashedKey);
                command.Parameters.AddWithValue("@ext", extJson);
                command.ExecuteNonQuery();
            }
        }

        public void Clear()
        {
            _cacheEntries.Clear();
            _ext.Clear();
            _updated = true;
        }

        public string? GetCacheEntry(string key)
        {
            if (_cacheEntries.TryGetValue(key, out string? value))
            {
                return value;
            }

            return null;
        }

        public void PutCacheEntry(string key, string entry)
        {
            _cacheEntries[key] = entry;
            _updated = true;
        }

        public string? GetExt(string key)
        {
            if (key.StartsWith(INTERNAL_EXT_PREFIX))
            {
                throw new ArgumentException($"Key '{key}' cannot start with an underscore.", nameof(key));
            }

            if (_ext.TryGetValue(key, out string? value))
            {
                return value;
            }

            return null;
        }

        public void PutExt(string key, string entry)
        {
            if (key.StartsWith(INTERNAL_EXT_PREFIX))
            {
                throw new ArgumentException($"Key '{key}' cannot start with an underscore.", nameof(key));
            }

            _ext[key] = entry;
            _updated = true;
        }
    }
}
