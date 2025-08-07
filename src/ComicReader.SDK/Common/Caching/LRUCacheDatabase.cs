// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using ComicReader.SDK.Common.DebugTools;

using Microsoft.Data.Sqlite;

namespace ComicReader.SDK.Common.Caching;

internal class LRUCacheDatabase(string filePath)
{
    public const string TAG = "LRUCacheDatabase";
    public const string CACHE_TABLE = "cache";
    public const string CACHE_TABLE_FIELD_KEY = "key";
    public const string CACHE_TABLE_FIELD_LAST_USED = "last_used";

    private readonly string _filePath = filePath;
    private readonly object _databaseLock = new();
    private SqliteConnection? _connection;

    public void Clear()
    {
        lock (_databaseLock)
        {
            SqliteConnection? connection = GetConnectionNoLock();
            if (connection is not null)
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "DELETE FROM " + CACHE_TABLE;
                command.ExecuteNonQuery();
            }
        }
    }

    public Dictionary<string, long>? BatchQuery(IEnumerable<string> keys)
    {
        Dictionary<string, long> results = [];
        foreach (string key in keys)
        {
            results[key] = -1;
        }

        lock (_databaseLock)
        {
            SqliteConnection? connection = GetConnectionNoLock();
            if (connection == null)
            {
                return null;
            }

            using SqliteCommand command = connection.CreateCommand();
            StringBuilder commandText = new($"SELECT {CACHE_TABLE_FIELD_KEY},{CACHE_TABLE_FIELD_LAST_USED} FROM {CACHE_TABLE} WHERE {CACHE_TABLE_FIELD_KEY} in (");
            int index = 0;
            foreach (string key in results.Keys)
            {
                string paramName = $"@key{index}";
                command.Parameters.AddWithValue(paramName, key);
                if (index > 0)
                {
                    commandText.Append(',');
                }
                commandText.Append(paramName);
                index++;
            }
            commandText.Append(')');
#pragma warning disable CA2100
            command.CommandText = commandText.ToString();
#pragma warning restore CA2100

            using SqliteDataReader query = command.ExecuteReader();
            while (query.Read())
            {
                string key = query.GetString(0);
                long lastUsed = query.GetInt64(1);
                results[key] = lastUsed;
            }
        }

        return results;
    }

    public void BatchUpdate(IDictionary<string, long> request)
    {
        lock (_databaseLock)
        {
            SqliteConnection? connection = GetConnectionNoLock();
            if (connection == null)
            {
                return;
            }

            using SqliteTransaction transaction = connection.BeginTransaction();
            foreach (KeyValuePair<string, long> pair in request)
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = $"INSERT OR REPLACE INTO {CACHE_TABLE}({CACHE_TABLE_FIELD_KEY},{CACHE_TABLE_FIELD_LAST_USED}) VALUES(@key,@value)";
                command.Parameters.AddWithValue("@key", pair.Key);
                command.Parameters.AddWithValue("@value", pair.Value);
                command.ExecuteNonQuery();
            }

            transaction.Commit();
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
            _connection = CreateConnection(false).Result;
        }
        catch (Exception ex)
        {
            Logger.F(TAG, "GetConnectionNoLock", ex);
        }

        if (_connection == null)
        {
            try
            {
                _connection = CreateConnection(true).Result;
            }
            catch (Exception ex)
            {
                Logger.F(TAG, "GetConnectionNoLock", ex);
            }
        }

        return _connection;
    }

    private async Task<SqliteConnection?> CreateConnection(bool clear)
    {
        if (clear || !File.Exists(_filePath))
        {
            try
            {
                File.Create(_filePath).Dispose();
            }
            catch (Exception ex)
            {
                Logger.F(TAG, "CreateConnection", ex);
                return null;
            }
        }

        var connection = new SqliteConnection($"Filename={_filePath}");
        connection.Open();
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE IF NOT EXISTS " + CACHE_TABLE + " (" +
                CACHE_TABLE_FIELD_KEY + " TEXT PRIMARY KEY," +
                CACHE_TABLE_FIELD_LAST_USED + " INTEGER NOT NULL)";
            await command.ExecuteNonQueryAsync();
        }

        return connection;
    }
}
