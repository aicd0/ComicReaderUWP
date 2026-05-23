// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;

using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Storage;
using ComicReaderUWP.Data.Models.Misc;

namespace ComicReaderUWP.Data.Database;

class DatabaseUpgradeManager
{
    private const string TAG = nameof(DatabaseUpgradeManager);
    private const int VERSION = 2;

    public static DatabaseUpgradeManager Instance = new();

    private static string VersionFilePath => Path.Combine(StorageLocation.LocalFolderPath, "version.txt");

    private DatabaseUpgradeManager() { }

    public void UpgradeDatabaseBeforeInitialization()
    {
        int version = ReadVersion();

        if (version < 0)
        {
            // New app
            File.WriteAllText(VersionFilePath, VERSION.ToString());
            version = VERSION;
        }

        if (version == VERSION)
        {
            return;
        }

        switch (version)
        {
            case 0:
                MoveFile(Path.Combine(StorageLocation.LocalFolderPath, "database.db"), Path.Combine(StorageLocation.LocalFolderPath, "database_sql", "main.db"));
                goto case 1;
            case 1: // 2.8.1
                MergeToDirectory(
                    Path.Combine(StorageLocation.LocalFolderPath, "database_sql"),
                    Path.Combine(StorageLocation.LocalFolderPath, "sqlite"));
                MergeToDirectory(
                    Path.Combine(StorageLocation.LocalFolderPath, "database_common"),
                    Path.Combine(StorageLocation.LocalFolderPath, "configs"));
                break;
            default:
                break;
        }

        File.WriteAllText(VersionFilePath, VERSION.ToString());
    }

    public void UpgradeDatabaseAfterInitialization()
    {
        DatabaseVersionModel.ExternalModel databaseVersions = DatabaseVersionModel.Instance.GetModel();
        List<Func<DatabaseVersionModel.ExternalModel, bool>> tasks = [
            UpgradeVersionModel,
            UpgradeKVStore,
            UpgradeSqliteDatabase,
            UpgradeFavorites,
            UpgradeHistory,
            UpgradeAppSettings,
        ];

        foreach (Func<DatabaseVersionModel.ExternalModel, bool> task in tasks)
        {
            if (task(databaseVersions))
            {
                DatabaseVersionModel.Instance.UpdateModel(databaseVersions);
            }
        }
    }

    private bool UpgradeVersionModel(DatabaseVersionModel.ExternalModel versions)
    {
        if (versions.Version >= DatabaseVersionModel.VERSION)
        {
            return false;
        }

        versions.Version = DatabaseVersionModel.VERSION;
        return true;
    }

    private bool UpgradeKVStore(DatabaseVersionModel.ExternalModel versions)
    {
        if (versions.KVStoreVersion >= DatabaseVersionModel.KV_STORE_VERSION)
        {
            return false;
        }

        switch (versions.KVStoreVersion)
        {
            case 0: // 2.8.1
                AppDB.AppKV.GetCollection(KVNames.KV_LIB_TIPS).Set(KVNames.KV_KEY_TIPS_READER_TIP_SHOWN, false);
                break;
            default:
                break;
        }

        versions.KVStoreVersion = DatabaseVersionModel.KV_STORE_VERSION;
        return true;
    }

    private bool UpgradeSqliteDatabase(DatabaseVersionModel.ExternalModel versions)
    {
        if (versions.SqliteDatabaseVersion >= DatabaseVersionModel.SQLITE_DATABASE_VERSION)
        {
            return false;
        }

        SqliteDB.UpdateDatabase(versions.SqliteDatabaseVersion);
        versions.SqliteDatabaseVersion = DatabaseVersionModel.SQLITE_DATABASE_VERSION;
        return true;
    }

    private bool UpgradeFavorites(DatabaseVersionModel.ExternalModel versions)
    {
        if (versions.FavoritesVersion >= DatabaseVersionModel.FAVORITES_VERSION)
        {
            return false;
        }

        versions.FavoritesVersion = DatabaseVersionModel.FAVORITES_VERSION;
        return true;
    }

    private bool UpgradeHistory(DatabaseVersionModel.ExternalModel versions)
    {
        if (versions.HistoryVersion >= DatabaseVersionModel.HISTORY_VERSION)
        {
            return false;
        }

        versions.HistoryVersion = DatabaseVersionModel.HISTORY_VERSION;
        return true;
    }

    private bool UpgradeAppSettings(DatabaseVersionModel.ExternalModel versions)
    {
        if (versions.AppSettingsVersion >= DatabaseVersionModel.APP_SETTING_VERSION)
        {
            return false;
        }

        versions.AppSettingsVersion = DatabaseVersionModel.APP_SETTING_VERSION;
        return true;
    }

    private static int ReadVersion()
    {
        string versionFile = VersionFilePath;
        if (!File.Exists(versionFile))
        {
            return -1;
        }

        string versionContent;
        try
        {
            versionContent = File.ReadAllText(versionFile);
        }
        catch (Exception e)
        {
            Logger.E(TAG, e);
            return -1;
        }

        if (!int.TryParse(versionContent, out int version))
        {
            return -1;
        }

        return version;
    }

    private static void MergeToDirectory(string sourceDir, string destinationDir)
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }

        Directory.CreateDirectory(destinationDir);

        foreach (string sourceFile in Directory.GetFiles(sourceDir))
        {
            string fileName = Path.GetFileName(sourceFile);
            string destFile = Path.Combine(destinationDir, fileName);

            if (File.Exists(destFile))
            {
                throw new IOException($"File already exists: {destFile}");
            }

            File.Move(sourceFile, destFile);
        }

        foreach (string sourceSubDir in Directory.GetDirectories(sourceDir))
        {
            string dirName = Path.GetFileName(sourceSubDir);
            string destSubDir = Path.Combine(destinationDir, dirName);
            MergeToDirectory(sourceSubDir, destSubDir);
        }

        Directory.Delete(sourceDir);
    }

    private static void MoveFile(string sourceFile, string destinationFile)
    {
        if (!File.Exists(sourceFile))
        {
            return;
        }

        if (File.Exists(destinationFile))
        {
            throw new IOException("File already exists at the new path: " + destinationFile);
        }

        string? newDir = Path.GetDirectoryName(destinationFile);
        if (newDir != null && !Directory.Exists(newDir))
        {
            Directory.CreateDirectory(newDir);
        }

        File.Move(sourceFile, destinationFile);
    }
}
