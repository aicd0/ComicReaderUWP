// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using ComicReader.SDK.Common.Storage;

using LiteDB;

namespace ComicReader.SDK.Database.KV;

/// <summary>
/// Backward compatibility. Will be removed in future versions.
/// </summary>
internal partial class OldLiteDBLayer(string prefix) : IDatabaseLayer
{
    private const string DEFAULT_COLLECTION = "default";

    private readonly string _prefix = prefix;
    private readonly ConcurrentDictionary<string, KVCollection> _collectionCache = [];

    public void Dispose()
    {
        foreach (KVCollection collection in _collectionCache.Values)
        {
            collection.Dispose();
        }

        _collectionCache.Clear();
    }

    public IKVCollection GetCollection(string name)
    {
        if (_collectionCache.TryGetValue(name, out KVCollection? cachedCollection))
        {
            return cachedCollection;
        }

        KVCollection collection = new($"{_prefix}_{name}.db");
        if (_collectionCache.TryAdd(name, collection))
        {
            return collection;
        }

        return _collectionCache[name];
    }

    private partial class KVCollection(string fileName) : IKVCollection, IDisposable
    {
        private readonly Lazy<LiteDatabase?> _db = new(() =>
        {
            string databaseFolder = Path.Combine(StorageLocation.LocalFolderPath, "database_kv");
            string databasePath = Path.Combine(databaseFolder, fileName);
            if (!File.Exists(databasePath))
            {
                return null;
            }

            return new LiteDatabase(databasePath);
        });

        public void Dispose()
        {
            if (_db.IsValueCreated)
            {
                _db.Value?.Dispose();
            }
        }

        public bool TryGet<T>(string key, [NotNullWhen(true)] out T? value)
        {
            LiteDatabase? db = _db.Value;
            if (db is null)
            {
                value = default;
                return false;
            }

            ILiteCollection<KVPair> col = db.GetCollection<KVPair>(DEFAULT_COLLECTION);
            KVPair pair = col.FindById(key);
            if (pair is null)
            {
                value = default;
                return false;
            }

            if (typeof(T) == typeof(int))
            {
                if (int.TryParse(pair.Value, out int typedValue))
                {
                    value = (T)(object)typedValue;
                    return true;
                }
            }
            else if (typeof(T) == typeof(long))
            {
                if (long.TryParse(pair.Value, out long typedValue))
                {
                    value = (T)(object)typedValue;
                    return true;
                }
            }
            else if (typeof(T) == typeof(float))
            {
                if (float.TryParse(pair.Value, out float typedValue))
                {
                    value = (T)(object)typedValue;
                    return true;
                }
            }
            else if (typeof(T) == typeof(double))
            {
                if (double.TryParse(pair.Value, out double typedValue))
                {
                    value = (T)(object)typedValue;
                    return true;
                }
            }
            else if (typeof(T) == typeof(string))
            {
                value = (T)(object)pair.Value;
                return true;
            }
            else if (typeof(T) == typeof(bool))
            {
                if (bool.TryParse(pair.Value, out bool typedValue))
                {
                    value = (T)(object)typedValue;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public void Set<T>(string key, T? value)
        {
            LiteDatabase? db = _db.Value;
            if (db is null)
            {
                return;
            }

            if (value is null)
            {
                db.GetCollection<KVPair>(DEFAULT_COLLECTION).Delete(key);
                return;
            }

            throw new InvalidOperationException();
        }
    }

    private class KVPair
    {
        [BsonId]
        public required string Key { get; set; }

        public required string Value { get; set; }
    }
}
