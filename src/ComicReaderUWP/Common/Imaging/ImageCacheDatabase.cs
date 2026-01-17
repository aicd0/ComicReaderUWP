// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Utils;

using Microsoft.Data.Sqlite;

namespace ComicReaderUWP.Common.Imaging;

internal class ImageCacheDatabase(string databaseFilePath)
{
    private const string TAG = nameof(ImageCacheDatabase);
    private const string MAIN_TABLE = "Main";
    private const string MAIN_TABLE_FIELD_KEY = "Key";
    private const string MAIN_TABLE_FIELD_EXT = "Ext";

    private readonly object _databaseLock = new();
    private SqliteConnection? _connection;
    private readonly ConcurrentDictionary<string, CacheRecord> _recordCache = [];

    public void Clear()
    {
        lock (_databaseLock)
        {
            _recordCache.Clear();

            SqliteConnection? connection = GetConnectionNoLock();
            if (connection is not null)
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "DELETE FROM " + MAIN_TABLE;
                command.ExecuteNonQuery();
            }
        }
    }

    public CacheRecord? GetOrCreate(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        string hashedKey = ToHashedKey(key);
        CacheRecord? record = Get(hashedKey, key);
        record ??= CacheRecord.CreateNew(this, key);
        return _recordCache.GetOrAdd(hashedKey, record);
    }

    private CacheRecord? Get(string hashedKey, string key)
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
                    $"{MAIN_TABLE_FIELD_EXT}" +
                    $" FROM {MAIN_TABLE} WHERE {MAIN_TABLE_FIELD_KEY}=@key";
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
                    catch (Exception e)
                    {
                        Logger.E(TAG, "CacheRecord", e);
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

    private async Task<SqliteConnection?> CreateConnection(bool clear)
    {
        string? databaseFolderPath = Path.GetDirectoryName(databaseFilePath);
        if (string.IsNullOrEmpty(databaseFolderPath))
        {
            Logger.F(TAG, "Database folder path is null or empty.");
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
                MAIN_TABLE_FIELD_KEY + " TEXT PRIMARY KEY," +
                MAIN_TABLE_FIELD_EXT + " TEXT)";
            await command.ExecuteNonQueryAsync();
        }

        return connection;
    }

    public class CacheRecord
    {
        private const string INTERNAL_EXT_PREFIX = "_";
        private const string CACHE_ENTRY_PREFIX = "_CacheEntry_";
        public const string IMAGE_CACHE_FINGERPRINT = "_ImageCacheFingerprint";

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
                else if (entry.Key == IMAGE_CACHE_FINGERPRINT)
                {
                    record._imageCacheFingerprint = entry.Value;
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
        private string _imageCacheFingerprint = string.Empty;
        private readonly Dictionary<string, string> _cacheEntries = [];
        private readonly Dictionary<string, string> _ext = [];

        public ReaderWriterLock Lock { get; } = new();

        public string ImageCacheFingerprint
        {
            get => _imageCacheFingerprint;
            set
            {
                if (_imageCacheFingerprint != value)
                {
                    _imageCacheFingerprint = value;
                    _updated = true;
                }
            }
        }

        private CacheRecord(ImageCacheDatabase db, string key)
        {
            _database = db;
            _key = key;
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
                Dictionary<string, string> ext = new(_ext)
                {
                    [IMAGE_CACHE_FINGERPRINT] = _imageCacheFingerprint,
                };

                foreach (KeyValuePair<string, string> entry in _cacheEntries)
                {
                    ext[CACHE_ENTRY_PREFIX + entry.Key] = entry.Value;
                }

                string extJson = JsonSerializer.Serialize(ext);

                SqliteConnection? connection = _database.GetConnectionNoLock();
                if (connection is null)
                {
                    Logger.F(TAG, $"Failed to save cache {_key}, unable to create database connection.");
                    return;
                }

                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = $"INSERT OR REPLACE INTO {MAIN_TABLE}({MAIN_TABLE_FIELD_KEY}" +
                    $",{MAIN_TABLE_FIELD_EXT}" +
                    $") VALUES(@key,@ext)";
                command.Parameters.AddWithValue("@key", hashedKey);
                command.Parameters.AddWithValue("@ext", extJson);
                command.ExecuteNonQuery();
            }
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
