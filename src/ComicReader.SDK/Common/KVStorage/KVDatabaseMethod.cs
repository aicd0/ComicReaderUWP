// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.KVStorage;

public abstract class KVDatabaseMethod : IDisposable
{
    private readonly Dictionary<string, KVDatabaseLib> _libs = [];

    public abstract void Dispose();

    public KVDatabaseLib With(string libName)
    {
        if (_libs.TryGetValue(libName, out KVDatabaseLib? lib))
        {
            return lib;
        }

        lib = new KVDatabaseLib(this, libName);
        _libs.Add(libName, lib);
        return lib;
    }

    public abstract void Remove(string lib, string key);

    public abstract void SetString(string lib, string key, string value);

    public abstract string? GetString(string lib, string key);

    public string GetString(string lib, string key, string defaultValue)
    {
        string? value = GetString(lib, key);
        if (value != null)
        {
            return value;
        }

        return defaultValue;
    }

    public abstract void SetBoolean(string lib, string key, bool value);

    public abstract bool? GetBoolean(string lib, string key);

    public bool GetBoolean(string lib, string key, bool defaultValue)
    {
        bool? value = GetBoolean(lib, key);
        if (value.HasValue)
        {
            return value.Value;
        }

        return defaultValue;
    }

    public abstract void SetLong(string lib, string key, long value);

    public abstract long? GetLong(string lib, string key);

    public long GetLong(string lib, string key, long defaultValue)
    {
        long? value = GetLong(lib, key);
        if (value.HasValue)
        {
            return value.Value;
        }

        return defaultValue;
    }

    public abstract void SetDouble(string lib, string key, double value);

    public abstract double? GetDouble(string lib, string key);

    public double GetDouble(string lib, string key, double defaultValue)
    {
        double? value = GetDouble(lib, key);
        if (value.HasValue)
        {
            return value.Value;
        }

        return defaultValue;
    }
}
