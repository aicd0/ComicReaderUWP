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
    private const int DATABASE_VERSION = 1;

    private const string META_TABLE = "meta";
    private const string META_COLUMN_KEY = "key";
    private const string META_COLUMN_VALUE = "value";
    private const string META_VERSION_KEY = "Version";

    private const string CACHE_TABLE = "cache";
    private const string URI_CACHE_KEY_TABLE = "uri_cache_key";
    private const string COLUMN_LAST_USED = "last_used";
    private const string COLUMN_KEY = "key";
    private const string COLUMN_EXT = "ext";
    private const string COLUMN_URI = "uri";
    private const string COLUMN_CACHE_KEY = "cache_key";

    private const long TOUCH_INTERVAL_SECONDS = 60 * 60;
    private const int TOUCH_FLUSH_DELAY_MS = 1000;
    private const int CLEANUP_BATCH_SIZE = 5000;

    private static void UpdateDatabase(SqliteConnection connection)
    {
        ExecuteCommand(connection, $"CREATE TABLE IF NOT EXISTS {META_TABLE} ({META_COLUMN_KEY} TEXT PRIMARY KEY, {META_COLUMN_VALUE} TEXT)");

        int databaseVersion = GetDatabaseVersion(connection);
        if (databaseVersion >= DATABASE_VERSION)
        {
            return;
        }

        switch (databaseVersion)
        {
            case 0:
                ExecuteCommand(connection, "CREATE TABLE IF NOT EXISTS cache (key TEXT PRIMARY KEY, ext TEXT, last_used INTEGER NOT NULL DEFAULT 0)");
                if (TableExists(connection, "Main"))
                {
                    ExecuteCommand(connection, "INSERT OR REPLACE INTO cache (key,ext,last_used) SELECT Key,Ext,@now FROM Main", ("@now", Now()));
                    ExecuteCommand(connection, "DROP TABLE Main");
                }

                if (!TableExists(connection, "uri_cache_key"))
                {
                    ExecuteCommand(connection, "CREATE TABLE uri_cache_key (uri TEXT PRIMARY KEY, cache_key TEXT, last_used INTEGER NOT NULL DEFAULT 0)");
                }
                else
                {
                    bool hasLastUsedColumn = false;
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        // The columns of table_info are: cid, name, type, notnull, dflt_value, pk.
                        command.CommandText = "PRAGMA table_info(uri_cache_key)";
                        using SqliteDataReader query = command.ExecuteReader();
                        while (query.Read())
                        {
                            if (query.GetString(1) == "last_used")
                            {
                                hasLastUsedColumn = true;
                                break;
                            }
                        }
                    }

                    if (!hasLastUsedColumn)
                    {
                        ExecuteCommand(connection, "ALTER TABLE uri_cache_key ADD COLUMN last_used INTEGER NOT NULL DEFAULT 0");
                        ExecuteCommand(connection, "UPDATE uri_cache_key SET last_used=@now", ("@now", Now()));
                    }
                }

                goto case 1;
            case 1:
                break;
            default:
                Logger.F(TAG, $"Unknown database version: {databaseVersion}");
                return;
        }

        SetDatabaseVersion(connection, DATABASE_VERSION);
    }

    private static int GetDatabaseVersion(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {META_COLUMN_VALUE} FROM {META_TABLE} WHERE {META_COLUMN_KEY}='{META_VERSION_KEY}'";
        using SqliteDataReader query = command.ExecuteReader();
        if (!query.Read())
        {
            return 0;
        }

        return int.TryParse(query.GetString(0), out int version) ? version : 0;
    }

    private static void SetDatabaseVersion(SqliteConnection connection, int version)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"INSERT INTO {META_TABLE} ({META_COLUMN_KEY},{META_COLUMN_VALUE}) VALUES ('{META_VERSION_KEY}',@version)" +
            $" ON CONFLICT({META_COLUMN_KEY}) DO UPDATE SET {META_COLUMN_VALUE}=@version";
        command.Parameters.AddWithValue("@version", version.ToString());
        command.ExecuteNonQuery();
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@name";
        command.Parameters.AddWithValue("@name", tableName);
        using SqliteDataReader query = command.ExecuteReader();
        return query.Read();
    }

    private static void ExecuteCommand(SqliteConnection connection, string commandText,
        params (string Name, object Value)[] parameters)
    {
        using SqliteCommand command = connection.CreateCommand();
#pragma warning disable CA2100
        command.CommandText = commandText;
#pragma warning restore CA2100
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        command.ExecuteNonQuery();
    }

    private static string ToHashedKey(string key)
    {
        byte[] hash = HashUtils.GetXxHash64(key);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static long Now()
    {
        return DateTimeOffset.Now.ToUnixTimeSeconds();
    }

    private readonly Lock _databaseLock = new();
    private SqliteConnection? _connection;
    private readonly ConcurrentDictionary<string, CacheRecord> _records = [];
    private readonly ConcurrentDictionary<string, UriMapping> _uriMappings = [];

    private readonly Lock _touchLock = new();
    private readonly Dictionary<string, long> _touchedRecords = [];
    private readonly Dictionary<string, long> _touchedUriMappings = [];
    private bool _postTouchFlushTask;

    public void Clear()
    {
        lock (_touchLock)
        {
            _touchedRecords.Clear();
            _touchedUriMappings.Clear();
        }

        lock (_databaseLock)
        {
            _records.Clear();
            _uriMappings.Clear();

            SqliteConnection? connection = GetConnectionNoLock();
            if (connection is not null)
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = $"DELETE FROM {CACHE_TABLE};DELETE FROM {URI_CACHE_KEY_TABLE}";
                command.ExecuteNonQuery();
            }
        }
    }

    public void Cleanup(TimeSpan ttl, int maxCacheRows)
    {
        long cutoff = Now() - (long)ttl.TotalSeconds;

        lock (_databaseLock)
        {
            SqliteConnection? connection = GetConnectionNoLock();
            if (connection is null)
            {
                return;
            }

            List<string> removedKeys = [];
            List<string> removedUris = [];
            try
            {
                using SqliteTransaction transaction = connection.BeginTransaction();

                int cacheRowCount;
                using (SqliteCommand countCommand = connection.CreateCommand())
                {
                    countCommand.CommandText = $"SELECT COUNT(*) FROM {CACHE_TABLE}";
                    using SqliteDataReader countQuery = countCommand.ExecuteReader();
                    cacheRowCount = countQuery.Read() ? countQuery.GetInt32(0) : 0;
                }

                // SQLite does not support DELETE ... LIMIT, so expired rows are deleted through a limited subquery.
                while (cacheRowCount > maxCacheRows)
                {
                    using SqliteCommand command = connection.CreateCommand();
                    command.CommandText =
                        $"DELETE FROM {CACHE_TABLE} WHERE {COLUMN_KEY} IN (" +
                        $"SELECT {COLUMN_KEY} FROM {CACHE_TABLE} WHERE {COLUMN_LAST_USED}<@cutoff" +
                        $" ORDER BY {COLUMN_LAST_USED} ASC LIMIT @limit) RETURNING {COLUMN_KEY}";
                    command.Parameters.AddWithValue("@cutoff", cutoff);
                    command.Parameters.AddWithValue("@limit", CLEANUP_BATCH_SIZE);
                    List<string> keys = [];
                    using (SqliteDataReader query = command.ExecuteReader())
                    {
                        while (query.Read())
                        {
                            keys.Add(query.GetString(0));
                        }
                    }

                    if (keys.Count == 0)
                    {
                        break;
                    }

                    cacheRowCount -= keys.Count;
                    removedKeys.AddRange(keys);
                }

                int uriRowCount;
                using (SqliteCommand countCommand = connection.CreateCommand())
                {
                    countCommand.CommandText = $"SELECT COUNT(*) FROM {URI_CACHE_KEY_TABLE}";
                    using SqliteDataReader countQuery = countCommand.ExecuteReader();
                    uriRowCount = countQuery.Read() ? countQuery.GetInt32(0) : 0;
                }

                while (uriRowCount > maxCacheRows)
                {
                    using SqliteCommand command = connection.CreateCommand();
                    command.CommandText =
                        $"DELETE FROM {URI_CACHE_KEY_TABLE} WHERE {COLUMN_URI} IN (" +
                        $"SELECT {COLUMN_URI} FROM {URI_CACHE_KEY_TABLE} WHERE {COLUMN_LAST_USED}<@cutoff" +
                        $" ORDER BY {COLUMN_LAST_USED} ASC LIMIT @limit) RETURNING {COLUMN_URI}";
                    command.Parameters.AddWithValue("@cutoff", cutoff);
                    command.Parameters.AddWithValue("@limit", CLEANUP_BATCH_SIZE);
                    List<string> uris = [];
                    using (SqliteDataReader query = command.ExecuteReader())
                    {
                        while (query.Read())
                        {
                            uris.Add(query.GetString(0));
                        }
                    }

                    if (uris.Count == 0)
                    {
                        break;
                    }

                    uriRowCount -= uris.Count;
                    removedUris.AddRange(uris);
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                Logger.F(TAG, nameof(Cleanup), ex);
                return;
            }

            foreach (string hashedKey in removedKeys)
            {
                _records.TryRemove(hashedKey, out _);
            }

            foreach (string hashedUri in removedUris)
            {
                _uriMappings.TryRemove(hashedUri, out _);
            }

            if (removedKeys.Count > 0 || removedUris.Count > 0)
            {
                Logger.I(TAG, $"Cache maintenance removed {removedKeys.Count} cache record(s) and {removedUris.Count} uri mapping(s)");
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
        return _records.GetOrAdd(hashedKey, record);
    }

    public string? GetCacheKey(string? uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return null;
        }

        string hashedUri = ToHashedKey(uri);
        if (_uriMappings.TryGetValue(hashedUri, out UriMapping? mapping))
        {
            TouchUriMapping(hashedUri, mapping);
            return mapping.CacheKey;
        }

        lock (_databaseLock)
        {
            if (_uriMappings.TryGetValue(hashedUri, out mapping))
            {
                TouchUriMapping(hashedUri, mapping);
                return mapping.CacheKey;
            }

            SqliteConnection? connection = GetConnectionNoLock();
            if (connection is null)
            {
                return null;
            }

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {COLUMN_CACHE_KEY},{COLUMN_LAST_USED} FROM {URI_CACHE_KEY_TABLE} WHERE {COLUMN_URI}=@uri";
            command.Parameters.AddWithValue("@uri", hashedUri);
            string cacheKey;
            long lastUsed;
            using (SqliteDataReader query = command.ExecuteReader())
            {
                if (!query.Read())
                {
                    return null;
                }

                cacheKey = query.GetString(0);
                lastUsed = query.GetInt64(1);
            }

            UriMapping loadedMapping = new(cacheKey, lastUsed);
            _uriMappings[hashedUri] = loadedMapping;
            TouchUriMapping(hashedUri, loadedMapping);
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
        long now = Now();
        if (_uriMappings.TryGetValue(hashedUri, out UriMapping? existing) && existing.CacheKey == cacheKey)
        {
            TouchUriMapping(hashedUri, existing);
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
                command.CommandText = $"INSERT INTO {URI_CACHE_KEY_TABLE} ({COLUMN_URI},{COLUMN_CACHE_KEY},{COLUMN_LAST_USED}) VALUES (@uri,@cacheKey,@lastUsed) " +
                    $"ON CONFLICT({COLUMN_URI}) DO UPDATE SET {COLUMN_CACHE_KEY}=@cacheKey,{COLUMN_LAST_USED}=@lastUsed";
                command.Parameters.AddWithValue("@uri", hashedUri);
                command.Parameters.AddWithValue("@cacheKey", cacheKey);
                command.Parameters.AddWithValue("@lastUsed", now);
                command.ExecuteNonQuery();
            }

            _uriMappings[hashedUri] = new UriMapping(cacheKey, now);
        }
    }

    private CacheRecord? GetCache(string hashedKey, string key)
    {
        {
            if (_records.TryGetValue(hashedKey, out CacheRecord? record))
            {
                TouchRecord(record);
                return record;
            }
        }

        lock (_databaseLock)
        {
            {
                if (_records.TryGetValue(hashedKey, out CacheRecord? record))
                {
                    TouchRecord(record);
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
                command.CommandText = $"SELECT {COLUMN_EXT},{COLUMN_LAST_USED} FROM {CACHE_TABLE} WHERE {COLUMN_KEY}=@key";
                command.Parameters.AddWithValue("@key", hashedKey);
                using SqliteDataReader query = command.ExecuteReader();
                while (query.Read())
                {
                    string extJson = query.GetString(0);
                    long lastUsed = query.GetInt64(1);
                    Dictionary<string, string> ext;
                    try
                    {
                        ext = new(JsonSerializer.Deserialize<Dictionary<string, string>>(extJson) ?? []);
                    }
                    catch (Exception ex)
                    {
                        Logger.E(TAG, "CacheRecord", ex);
                        ext = [];
                    }

                    var record = CacheRecord.FromDatabase(this, key, ext, lastUsed);
                    records.Add(record);
                }
            }

            Logger.Assert(records.Count <= 1, "9C0107871C1B6CB1");
            if (records.Count == 0)
            {
                return null;
            }

            TouchRecord(records[0]);
            return records[0];
        }
    }

    private void TouchRecord(CacheRecord record)
    {
        long now = Now();
        if (now - record.LastUsed < TOUCH_INTERVAL_SECONDS)
        {
            return;
        }

        record.LastUsed = now;
        string hashedKey = record.HashedKey;
        lock (_touchLock)
        {
            _touchedRecords[hashedKey] = now;
        }

        PostTouchFlushTask();
    }

    private void TouchUriMapping(string hashedUri, UriMapping mapping)
    {
        long now = Now();
        if (now - mapping.LastUsed < TOUCH_INTERVAL_SECONDS)
        {
            return;
        }

        UriMapping updatedMapping = new(mapping.CacheKey, now);
        if (!_uriMappings.TryUpdate(hashedUri, updatedMapping, mapping))
        {
            return;
        }

        lock (_touchLock)
        {
            _touchedUriMappings[hashedUri] = now;
        }

        PostTouchFlushTask();
    }

    private void PostTouchFlushTask()
    {
        lock (_touchLock)
        {
            if (_postTouchFlushTask)
            {
                return;
            }

            _postTouchFlushTask = true;
        }

        CoroutineUtils.Run(async () =>
        {
            await Task.Delay(TOUCH_FLUSH_DELAY_MS);
            FlushPendingTouches();
        });
    }

    private void FlushPendingTouches()
    {
        Dictionary<string, long> recordTouches;
        Dictionary<string, long> uriTouches;
        lock (_touchLock)
        {
            recordTouches = new(_touchedRecords);
            uriTouches = new(_touchedUriMappings);
            _touchedRecords.Clear();
            _touchedUriMappings.Clear();
            _postTouchFlushTask = false;
        }

        if (recordTouches.Count == 0 && uriTouches.Count == 0)
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

            try
            {
                using SqliteTransaction transaction = connection.BeginTransaction();
                foreach (KeyValuePair<string, long> pair in recordTouches)
                {
                    using SqliteCommand command = connection.CreateCommand();
                    command.CommandText =
                        $"UPDATE {CACHE_TABLE} SET {COLUMN_LAST_USED}=MAX({COLUMN_LAST_USED},@lastUsed)" +
                        $" WHERE {COLUMN_KEY}=@key";
                    command.Parameters.AddWithValue("@key", pair.Key);
                    command.Parameters.AddWithValue("@lastUsed", pair.Value);
                    command.ExecuteNonQuery();
                }

                foreach (KeyValuePair<string, long> pair in uriTouches)
                {
                    using SqliteCommand command = connection.CreateCommand();
                    command.CommandText =
                        $"UPDATE {URI_CACHE_KEY_TABLE} SET {COLUMN_LAST_USED}=MAX({COLUMN_LAST_USED},@lastUsed)" +
                        $" WHERE {COLUMN_URI}=@key";
                    command.Parameters.AddWithValue("@key", pair.Key);
                    command.Parameters.AddWithValue("@lastUsed", pair.Value);
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                Logger.F(TAG, nameof(FlushPendingTouches), ex);
            }
        }
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
        UpdateDatabase(connection);

        return connection;
    }

    private sealed class UriMapping(string cacheKey, long lastUsed)
    {
        public string CacheKey { get; } = cacheKey;
        public long LastUsed { get; } = lastUsed;
    }

    public class CacheRecord
    {
        private const string INTERNAL_EXT_PREFIX = "_";
        private const string CACHE_ENTRY_PREFIX = "_CacheEntry_";

        public static CacheRecord FromDatabase(ImageCacheDatabase db, string key, IReadOnlyDictionary<string, string> ext, long lastUsed)
        {
            CacheRecord record = new(db, key)
            {
                _updated = false,
                LastUsed = lastUsed
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
            CacheRecord record = new(db, key)
            {
                _updated = true,
                LastUsed = Now()
            };
            return record;
        }

        private readonly ImageCacheDatabase _database;
        private readonly string _key;
        private bool _updated;
        private long _lastUsed;
        private readonly Dictionary<string, string> _cacheEntries = [];
        private readonly Dictionary<string, string> _ext = [];

        private readonly Lock _queueLock = new();
        private Task _queueTail = Task.CompletedTask;

        private CacheRecord(ImageCacheDatabase db, string key)
        {
            _database = db;
            _key = key;
        }

        public string HashedKey => ToHashedKey(_key);

        public long LastUsed
        {
            get => Interlocked.Read(ref _lastUsed);
            set => Interlocked.Exchange(ref _lastUsed, value);
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
                LastUsed = Now();

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
                command.CommandText = $"INSERT OR REPLACE INTO {CACHE_TABLE}({COLUMN_KEY}" +
                    $",{COLUMN_EXT}" +
                    $",{COLUMN_LAST_USED}" +
                    $") VALUES(@key,@ext,@lastUsed)";
                command.Parameters.AddWithValue("@key", hashedKey);
                command.Parameters.AddWithValue("@ext", extJson);
                command.Parameters.AddWithValue("@lastUsed", LastUsed);
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
