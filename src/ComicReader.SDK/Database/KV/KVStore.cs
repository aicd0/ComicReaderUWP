// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace ComicReader.SDK.Database.KV;

public static class KVStore
{
    private static readonly ConcurrentDictionary<string, IDatabaseLayer> sDatabases = [];

    public static void Dispose()
    {
        foreach (IDatabaseLayer database in sDatabases.Values)
        {
            database.Dispose();
        }

        sDatabases.Clear();
    }

    internal static IKVDatabase GetDatabase(string name)
    {
        if (sDatabases.TryGetValue(name, out IDatabaseLayer? database))
        {
            return database;
        }

        database = new CacheLayer(new LiteDBLayer(name, fallbackLayer: new OldLiteDBLayer(name)));
        if (sDatabases.TryAdd(name, database))
        {
            return database;
        }

        database.Dispose();
        return sDatabases[name];
    }

    //
    // Predefined Databases
    //

    public static IKVDatabase App => GetDatabase("app");
    internal static IKVDatabase Sdk => GetDatabase("sdk");

    public static IKVDatabase Plugin(string pluginName)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(pluginName);
        byte[] hash = SHA256.HashData(bytes);
        string hashString = Convert.ToHexString(hash)[..8].ToLower();
        return GetDatabase($"plugin_{hashString}");
    }
}
