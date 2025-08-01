// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.KVStorage;

public class KVDatabaseLib(KVDatabaseMethod method, string name)
{
    private readonly KVDatabaseMethod _method = method;
    private readonly string _name = name;

    public void SetString(string key, string value)
    {
        _method.SetString(_name, key, value);
    }

    public string? GetString(string key)
    {
        return _method.GetString(_name, key);
    }
}
