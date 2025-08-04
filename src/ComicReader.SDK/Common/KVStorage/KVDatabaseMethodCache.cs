// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;

namespace ComicReader.SDK.Common.KVStorage;

internal class KVDatabaseMethodCache(KVDatabaseMethod method) : KVDatabaseMethod
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, object?>> _cache = new();

    public override void Remove(string lib, string key)
    {
        RemoveValue(lib, key);
        method.Remove(lib, key);
    }

    public override void SetString(string lib, string key, string value)
    {
        SetValue(lib, key, value);
        method.SetString(lib, key, value);
    }

    public override string? GetString(string lib, string key)
    {
        if (GetClassValue(lib, key, out string? value))
        {
            return value;
        }

        value = method.GetString(lib, key);
        SetValue(lib, key, value);
        return value;
    }

    public override void SetBoolean(string lib, string key, bool value)
    {
        SetValue(lib, key, value);
        method.SetBoolean(lib, key, value);
    }

    public override bool? GetBoolean(string lib, string key)
    {
        if (GetPrimitiveValue(lib, key, out bool? value))
        {
            return value;
        }

        value = method.GetBoolean(lib, key);
        SetValue(lib, key, value);
        return value;
    }

    public override void SetLong(string lib, string key, long value)
    {
        SetValue(lib, key, value);
        method.SetLong(lib, key, value);
    }

    public override long? GetLong(string lib, string key)
    {
        if (GetPrimitiveValue(lib, key, out long? value))
        {
            return value;
        }

        value = method.GetLong(lib, key);
        SetValue(lib, key, value);
        return value;
    }

    public override void SetDouble(string lib, string key, double value)
    {
        SetValue(lib, key, value);
        method.SetDouble(lib, key, value);
    }

    public override double? GetDouble(string lib, string key)
    {
        if (GetPrimitiveValue(lib, key, out double? value))
        {
            return value;
        }

        value = method.GetDouble(lib, key);
        SetValue(lib, key, value);
        return value;
    }

    private bool GetPrimitiveValue<T>(string lib, string key, out T? value) where T : struct
    {
        if (!_cache.TryGetValue(lib, out ConcurrentDictionary<string, object?>? libDict))
        {
            value = null;
            return false;
        }

        if (!libDict.TryGetValue(key, out object? cacheValue))
        {
            value = null;
            return false;
        }

        if (cacheValue == null || cacheValue is not T)
        {
            value = null;
            return true;
        }

        value = (T)cacheValue;
        return true;
    }

    private bool GetClassValue<T>(string lib, string key, out T? value) where T : class
    {
        if (!_cache.TryGetValue(lib, out ConcurrentDictionary<string, object?>? libDict))
        {
            value = null;
            return false;
        }

        if (!libDict.TryGetValue(key, out object? cacheValue))
        {
            value = null;
            return false;
        }

        if (cacheValue == null || cacheValue is not T)
        {
            value = null;
            return true;
        }

        value = (T)cacheValue;
        return true;
    }

    private void SetValue(string lib, string key, object? value)
    {
        if (!_cache.TryGetValue(lib, out ConcurrentDictionary<string, object?>? libDict))
        {
            libDict = new ConcurrentDictionary<string, object?>();
            if (!_cache.TryAdd(lib, libDict))
            {
                libDict = _cache[lib];
            }
        }

        libDict[key] = value;
    }

    private void RemoveValue(string lib, string key)
    {
        if (!_cache.TryGetValue(lib, out ConcurrentDictionary<string, object?>? libDict))
        {
            return;
        }

        libDict.TryRemove(key, out _);
    }
}
