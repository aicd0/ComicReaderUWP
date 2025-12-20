// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace ComicReader.SDK.Common.KVStorage;

public static class KVDatabase
{
    private static readonly ConcurrentDictionary<string, KVDatabaseMethod> sDatabases = [];

    public static KVDatabaseMethod Default => GetDatabase("lib");
    internal static KVDatabaseMethod Sdk => GetDatabase("sdk");

    public static void Dispose()
    {
        foreach (KVDatabaseMethod method in sDatabases.Values)
        {
            method.Dispose();
        }

        sDatabases.Clear();
    }

    public static KVDatabaseMethod Plugin(string pluginName)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(pluginName);
        byte[] hash = SHA256.HashData(bytes);
        string hashString = Convert.ToHexString(hash)[..8].ToLower();
        return GetDatabase($"plugin_{hashString}");
    }

    private static KVDatabaseMethod GetDatabase(string name)
    {
        if (sDatabases.TryGetValue(name, out KVDatabaseMethod? method))
        {
            return method;
        }

        method = new KVDatabaseMethodCache(new KVDatabaseMethodLiteDB(name));
        if (sDatabases.TryAdd(name, method))
        {
            return method;
        }

        method.Dispose();
        return sDatabases[name];
    }
}
