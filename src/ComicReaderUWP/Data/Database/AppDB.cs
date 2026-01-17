// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.SDK.Common.Storage;
using ComicReaderUWP.SDK.Common.Utils;
using ComicReaderUWP.SDK.Database.KV;
using ComicReaderUWP.SDK.Database.Misc;
using ComicReaderUWP.SDK.Database.Registry;

namespace ComicReaderUWP.Data.Database;

internal static class AppDB
{
    //
    // Directories
    //

    private static string KvDirectory => Path.Combine(StorageLocation.LocalFolderPath, "kv");
    private static string RegistryDirectory => Path.Combine(StorageLocation.LocalFolderPath, "reg");

    //
    // Host Databases
    //

    private static readonly Lazy<IRegistryDatabase> _mainRegistryDatabase = new(() =>
    {
        string databasePath = Path.Combine(RegistryDirectory, "Main.db");
        return RegistryStore.CreateDatabase(databasePath);
    });
    public static IRegistryDatabase MainRegistry => _mainRegistryDatabase.Value;

    private static readonly Lazy<IKVDatabase> _appKvDatabase = new(() =>
    {
        string databasePath = Path.Combine(KvDirectory, $"app.db");
        return KVStore.CreateDatabase(databasePath, "lib");
    });
    public static IKVDatabase AppKV => _appKvDatabase.Value;

    //
    // Plugins Databases
    //

    private static readonly ConcurrentDictionary<string, IKVDatabase> sPluginKvDatabases = [];

    public static IKVDatabase PluginKV(string pluginName)
    {
        if (sPluginKvDatabases.TryGetValue(pluginName, out IKVDatabase? db))
        {
            return db;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(pluginName);
        byte[] hash = HashUtils.GetXxHash64(bytes);
        string hashString = Convert.ToHexString(hash)[..8].ToLowerInvariant();
        string databasePath = Path.Combine(KvDirectory, $"plugin_{hashString}.db");
        db = KVStore.CreateDatabase(databasePath);
        if (sPluginKvDatabases.TryAdd(pluginName, db))
        {
            return db;
        }

        return sPluginKvDatabases[pluginName];
    }

    //
    // Helpers
    //

    public static void Dispose()
    {
        SqliteDB.Dispose();

        if (_mainRegistryDatabase.IsValueCreated)
        {
            _mainRegistryDatabase.Value.Dispose();
        }

        if (_appKvDatabase.IsValueCreated)
        {
            _appKvDatabase.Value.Dispose();
        }

        foreach (IKVDatabase db in sPluginKvDatabases.Values)
        {
            db.Dispose();
        }

        sPluginKvDatabases.Clear();

        SdkDB.Dispose();
    }

    public static void Initialize()
    {
        MainRegistry.RemoveKey(RegistryNames.RUNTIME_RESOURCES);

        DatabaseUpgradeManager.Instance.UpgradeDatabaseBeforeInitialization();
        SqliteDB.Initialize();
        DatabaseUpgradeManager.Instance.UpgradeDatabaseAfterInitialization();
    }
}
