// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Core.Common.Storage;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Core.Database.KV;
using ComicReaderUWP.Core.Database.Misc;
using ComicReaderUWP.Core.Database.Registry;
using ComicReaderUWP.SDK.Models;

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
        return KVStore.CreateDatabase(databasePath, shared: false);
    });
    public static IKVDatabase AppKV => _appKvDatabase.Value;

    //
    // Plugins Databases
    //

    private static readonly ConcurrentDictionary<string, IRegistryDatabase> sPluginRegistryDatabases = [];

    public static IRegistryDatabase PluginRegistry(string pluginName)
    {
        if (sPluginRegistryDatabases.TryGetValue(pluginName, out IRegistryDatabase? db))
        {
            return db;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(pluginName);
        byte[] hash = HashUtils.GetXxHash64(bytes);
        string hashString = Convert.ToHexString(hash)[..8].ToUpperInvariant();
        string databasePath = Path.Combine(RegistryDirectory, $"PluginRegistry_{hashString}.db");
        db = RegistryStore.CreateDatabase(databasePath);
        if (sPluginRegistryDatabases.TryAdd(pluginName, db))
        {
            return db;
        }

        return sPluginRegistryDatabases[pluginName];
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

        foreach (IRegistryDatabase db in sPluginRegistryDatabases.Values)
        {
            db.Dispose();
        }

        sPluginRegistryDatabases.Clear();
        CoreDB.Dispose();
    }

    public static void Initialize()
    {
        MainRegistry.RemoveKey(RegistryNames.RUNTIME_RESOURCES);

        DatabaseUpgradeManager.Instance.UpgradeDatabaseBeforeInitialization();
        SqliteDB.Initialize();
        DatabaseUpgradeManager.Instance.UpgradeDatabaseAfterInitialization();
    }
}
