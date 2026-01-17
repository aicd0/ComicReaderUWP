// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;

using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.SDK.Common.Storage;
using ComicReaderUWP.SDK.Database.Registry;

namespace ComicReaderUWP.Data.Misc;

internal static class DatabaseManager
{
    private static string RegistryDirectory => Path.Combine(StorageLocation.LocalFolderPath, "reg");

    private static readonly Lazy<IRegistryDatabase> _mainRegistryDatabase = new(() =>
    {
        string databasePath = Path.Combine(RegistryDirectory, "Main.db");
        return RegistryStore.CreateDatabase(databasePath);
    });
    public static IRegistryDatabase MainRegistry => _mainRegistryDatabase.Value;

    public static void Dispose()
    {
        if (_mainRegistryDatabase.IsValueCreated)
        {
            _mainRegistryDatabase.Value.Dispose();
        }
    }

    public static void Initialize()
    {
        MainRegistry.RemoveKey(RegistryNames.RUNTIME_RESOURCES);

        DatabaseUpgradeManager.Instance.UpgradeDatabaseBeforeInitialization();
        SqlDatabaseManager.Initialize();
        DatabaseUpgradeManager.Instance.UpgradeDatabaseAfterInitialization();
    }
}
