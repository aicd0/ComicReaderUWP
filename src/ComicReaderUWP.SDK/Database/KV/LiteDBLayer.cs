// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using LiteDB;

namespace ComicReaderUWP.SDK.Database.KV;

internal partial class LiteDBLayer(string databasePath, IDatabaseLayer? fallbackLayer = null) : IDatabaseLayer
{
    private readonly Lazy<LiteDatabase> _db = new(() =>
    {
        string? databaseFolder = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(databaseFolder))
        {
            Directory.CreateDirectory(databaseFolder);
        }

        return new LiteDatabase($"Filename={databasePath}; Mode=Shared;");
    });

    private readonly IDatabaseLayer? _fallbackLayer = fallbackLayer;
    private readonly ConcurrentDictionary<string, KVCollection> _collectionCache = [];

    public void Dispose()
    {
        if (_db.IsValueCreated)
        {
            _db.Value.Dispose();
        }

        _fallbackLayer?.Dispose();
        _collectionCache.Clear();
    }

    public IKVCollection GetCollection(string name)
    {
        if (_collectionCache.TryGetValue(name, out KVCollection? cachedCollection))
        {
            return cachedCollection;
        }

        KVCollection collection = new(this, name);
        if (_collectionCache.TryAdd(name, collection))
        {
            return collection;
        }

        return _collectionCache[name];
    }

    private LiteDatabase GetDatabase()
    {
        return _db.Value;
    }

    private class KVCollection(LiteDBLayer layer, string name) : IKVCollection
    {
        private readonly Lazy<ILiteCollection<KvEntry>> _collection = new(() =>
        {
            LiteDatabase db = layer.GetDatabase();
            return db.GetCollection<KvEntry>(name);
        });

        public bool TryGet<T>(string key, [NotNullWhen(true)] out T? value)
        {
            if (TryGetNormal(key, out T? internalValue))
            {
                value = internalValue;
                return true;
            }

            IDatabaseLayer? fallbackLayer = layer._fallbackLayer;
            if (fallbackLayer is not null)
            {
                IKVCollection fallbackCollection = fallbackLayer.GetCollection(name);
                if (fallbackCollection.TryGet(key, out T? fallbackValue))
                {
                    Set(key, fallbackValue);
                    value = fallbackValue;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private bool TryGetNormal<T>(string key, [NotNullWhen(true)] out T? value)
        {
            ILiteCollection<KvEntry> collection = _collection.Value;
            KvEntry? entry = collection.FindById(key);
            if (entry is null || entry.Value.IsNull)
            {
                value = default;
                return false;
            }

            bool typeMatched = entry.Value.Type switch
            {
                BsonType.Int32 => typeof(T) == typeof(int) || typeof(T) == typeof(long),
                BsonType.Int64 => typeof(T) == typeof(long),
                BsonType.Double => typeof(T) == typeof(float) || typeof(T) == typeof(double),
                BsonType.Decimal => typeof(T) == typeof(decimal),
                BsonType.Binary => typeof(T) == typeof(byte[]),
                BsonType.String => typeof(T) == typeof(string),
                BsonType.Guid => typeof(T) == typeof(Guid),
                BsonType.Boolean => typeof(T) == typeof(bool),
                BsonType.DateTime => typeof(T) == typeof(DateTime),
                _ => false,
            };
            if (typeMatched)
            {
                value = (T)entry.Value.RawValue;
                return true;
            }

            value = default;
            return false;
        }

        public void Set<T>(string key, T? value)
        {
            // Clear from fallback layer
            layer._fallbackLayer?.GetCollection(name).Set<object>(key, null);

            ILiteCollection<KvEntry> collection = _collection.Value;
            if (value is null)
            {
                collection.Delete(key);
                return;
            }

            BsonValue bsonValue = value switch
            {
                int typedValue => new BsonValue(typedValue),
                long typedValue => new BsonValue(typedValue),
                float typedValue => new BsonValue((double)typedValue),
                double typedValue => new BsonValue(typedValue),
                decimal typedValue => new BsonValue(typedValue),
                byte[] typedValue => new BsonValue(typedValue),
                string typedValue => new BsonValue(typedValue),
                Guid typedValue => new BsonValue(typedValue),
                bool typedValue => new BsonValue(typedValue),
                DateTime typedValue => new BsonValue(typedValue),
                _ => throw new NotSupportedException($"Type {typeof(T)} is not supported."),
            };
            collection.Upsert(key, new KvEntry
            {
                Key = key,
                Value = bsonValue,
            });
        }
    }

    private class KvEntry
    {
        [BsonId]
        public required string Key { get; set; }

        public required BsonValue Value { get; set; }
    }
}
