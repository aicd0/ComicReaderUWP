// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ComicReaderUWP.SDK.Database.KV;

internal partial class CacheLayer(IDatabaseLayer innerLayer) : IDatabaseLayer
{
    private readonly ConcurrentDictionary<string, KVCollection> _cache = [];

    public void Dispose()
    {
        _cache.Clear();
        innerLayer.Dispose();
    }

    public IKVCollection GetCollection(string name)
    {
        if (_cache.TryGetValue(name, out KVCollection? cachedCollection))
        {
            return cachedCollection;
        }

        KVCollection collection = new(innerLayer.GetCollection(name));
        if (_cache.TryAdd(name, collection))
        {
            return collection;
        }

        return _cache[name];
    }

    private class KVCollection(IKVCollection innerCollection) : IKVCollection
    {
        private readonly ConcurrentDictionary<string, object?> _cache = [];

        public bool TryGet<T>(string key, [NotNullWhen(true)] out T? value)
        {
            if (_cache.TryGetValue(key, out object? cachedValue))
            {
                if (cachedValue is T typedValue)
                {
                    value = typedValue;
                    return true;
                }

                value = default;
                return false;
            }

            if (innerCollection.TryGet(key, out T? innerValue))
            {
                _cache[key] = innerValue;
                value = innerValue;
                return true;
            }

            _cache[key] = null;
            value = default;
            return false;
        }

        public void Set<T>(string key, T? value)
        {
            _cache[key] = value;
            innerCollection.Set(key, value);
        }
    }
}
