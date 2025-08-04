// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;

using ComicReader.SDK.Common.Storage;

using LiteDB;

namespace ComicReader.SDK.Common.KVStorage;

internal class KVDatabaseMethodLiteDB(string prefix) : KVDatabaseMethod, IDisposable
{
    private const string DEFAULT_COLLECTION = "default";

    private readonly object _lock = new();
    private readonly string _prefix = prefix;
    private readonly ConcurrentDictionary<string, LiteDatabase> _db = [];

    public void Dispose()
    {
        foreach (LiteDatabase db in _db.Values)
        {
            db.Dispose();
        }
    }

    public override void Remove(string lib, string key)
    {
        ILiteCollection<KVPair> col = GetDatabase(lib).GetCollection<KVPair>(DEFAULT_COLLECTION);
        col.Delete(key);
    }

    public override void SetString(string lib, string key, string value)
    {
        SetValue(lib, key, value);
    }

    public override string? GetString(string lib, string key)
    {
        return GetValue(lib, key);
    }

    public override void SetBoolean(string lib, string key, bool value)
    {
        string s = value ? "true" : "false";
        SetValue(lib, key, s);
    }

    public override bool? GetBoolean(string lib, string key)
    {
        string? s = GetValue(lib, key);
        if (s == null)
        {
            return null;
        }

        return s.Equals("true");
    }

    public override void SetLong(string lib, string key, long value)
    {
        string s = value.ToString();
        SetValue(lib, key, s);
    }

    public override long? GetLong(string lib, string key)
    {
        string? s = GetValue(lib, key);

        if (s == null)
        {
            return null;
        }

        if (long.TryParse(s, out long result))
        {
            return result;
        }

        return null;
    }

    public override void SetDouble(string lib, string key, double value)
    {
        string s = value.ToString();
        SetValue(lib, key, s);
    }

    public override double? GetDouble(string lib, string key)
    {
        string? s = GetValue(lib, key);

        if (s == null)
        {
            return null;
        }

        if (double.TryParse(s, out double result))
        {
            return result;
        }

        return null;
    }

    private LiteDatabase GetDatabase(string lib)
    {
        {
            if (_db.TryGetValue(lib, out LiteDatabase? db))
            {
                return db;
            }
        }

        lock (_lock)
        {
            if (_db.TryGetValue(lib, out LiteDatabase? db))
            {
                return db;
            }

            string databaseFolder = Path.Combine(StorageLocation.LocalFolderPath, "database_kv");
            string databasePath = Path.Combine(databaseFolder, $"{_prefix}_{lib}.db");
            Directory.CreateDirectory(databaseFolder);
            db = new LiteDatabase(databasePath);
            _db[lib] = db;
            return db;
        }
    }

    private string? GetValue(string lib, string key)
    {
        ILiteCollection<KVPair> col = GetDatabase(lib).GetCollection<KVPair>(DEFAULT_COLLECTION);
        KVPair pair = col.FindById(key);
        if (pair == null)
        {
            return null;
        }

        return pair.Value;
    }

    private void SetValue(string lib, string key, string value)
    {
        ILiteCollection<KVPair> col = GetDatabase(lib).GetCollection<KVPair>(DEFAULT_COLLECTION);
        KVPair pair = col.FindById(key);
        if (pair == null)
        {
            pair = new KVPair
            {
                Key = key,
                Value = value
            };

            col.Insert(pair);
        }
        else
        {
            if (pair.Value == value)
            {
                return;
            }

            pair.Value = value;
            col.Update(pair);
        }
    }

    private class KVPair
    {
        [BsonId]
        public required string Key { get; set; }

        public required string Value { get; set; }
    }
}
