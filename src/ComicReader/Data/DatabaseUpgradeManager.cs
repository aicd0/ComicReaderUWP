// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;

using ComicReader.Data.Models;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Storage;

namespace ComicReader.Data;

class DatabaseUpgradeManager
{
    private const string TAG = nameof(DatabaseUpgradeManager);
    private const int VERSION = 1;

    public static DatabaseUpgradeManager Instance = new();

    private static string VersionFilePath => Path.Combine(StorageLocation.LocalFolderPath, "version.txt");

    private DatabaseUpgradeManager() { }

    public void UpgradeDatabaseBeforeInitialization()
    {
        int version = ReadVersion();
        if (version == VERSION)
        {
            return;
        }

        switch (version)
        {
            case 0:
                {
                    string oldPath = Path.Combine(StorageLocation.LocalFolderPath, "database.db");
                    string newPath = Path.Combine(StorageLocation.LocalFolderPath, "database_sql", "main.db");
                    if (File.Exists(oldPath))
                    {
                        MoveFile(oldPath, newPath);
                    }
                }
                break;
            default:
                break;
        }

        File.WriteAllText(VersionFilePath, VERSION.ToString());
    }

    public void UpgradeDatabaseAfterInitialization()
    {
        DatabaseVersionModel.JsonModel databaseVersions = DatabaseVersionModel.Instance.GetModel();
        if (databaseVersions == null)
        {
            return;
        }

        List<Func<DatabaseVersionModel.JsonModel, bool>> tasks = [
            UpgradeDatabaseVersions,
            UpgradeComicDatabase,
            UpgradeFavorites,
            UpgradeHistory,
            UpgradeAppSettings,
        ];

        foreach (Func<DatabaseVersionModel.JsonModel, bool> task in tasks)
        {
            if (task(databaseVersions))
            {
                DatabaseVersionModel.Instance.UpdateModel(databaseVersions);
            }
        }
    }

    private bool UpgradeDatabaseVersions(DatabaseVersionModel.JsonModel versions)
    {
        if (versions.DatabaseVersionsVersion >= 1)
        {
            return false;
        }

        versions.DatabaseVersionsVersion = 1;
        return true;
    }

    private bool UpgradeComicDatabase(DatabaseVersionModel.JsonModel versions)
    {
        if (versions.ComicDatabaseVersion >= SqlDatabaseManager.DATABASE_VERSION)
        {
            return false;
        }

        SqlDatabaseManager.UpdateDatabase(versions.ComicDatabaseVersion);
        versions.ComicDatabaseVersion = SqlDatabaseManager.DATABASE_VERSION;
        return true;
    }

    private bool UpgradeFavorites(DatabaseVersionModel.JsonModel versions)
    {
        if (versions.FavoritesVersion >= 1)
        {
            return false;
        }

        versions.FavoritesVersion = 1;
        return true;
    }

    private bool UpgradeHistory(DatabaseVersionModel.JsonModel versions)
    {
        if (versions.HistoryVersion >= 1)
        {
            return false;
        }

        versions.HistoryVersion = 1;
        return true;
    }

    private bool UpgradeAppSettings(DatabaseVersionModel.JsonModel versions)
    {
        if (versions.AppSettingVersion >= 1)
        {
            return false;
        }

        versions.AppSettingVersion = 1;
        return true;
    }

    private static int ReadVersion()
    {
        string versionFile = VersionFilePath;
        if (!File.Exists(versionFile))
        {
            return 0;
        }

        string versionContent;
        try
        {
            versionContent = File.ReadAllText(versionFile);
        }
        catch (Exception e)
        {
            Logger.E(TAG, e);
            return 0;
        }

        if (int.TryParse(versionContent, out int version))
        {
            return version;
        }

        return 0;
    }

    private static void MoveFile(string oldPath, string newPath)
    {
        if (File.Exists(newPath))
        {
            throw new IOException("File already exists at the new path: " + newPath);
        }

        string? newDir = Path.GetDirectoryName(newPath);
        if (newDir != null && !Directory.Exists(newDir))
        {
            Directory.CreateDirectory(newDir);
        }

        File.Move(oldPath, newPath);
    }
}
