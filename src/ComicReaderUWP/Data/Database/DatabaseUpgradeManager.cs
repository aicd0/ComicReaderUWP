// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;

using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Core.Common.AppEnvironment;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Storage;

namespace ComicReaderUWP.Data.Database;

internal static class DatabaseUpgradeManager
{
    private const string TAG = nameof(DatabaseUpgradeManager);

    public static void UpgradeDatabaseBeforeInitialization()
    {
        string legacyVersionFilePath = Path.Combine(StorageLocation.LocalFolderPath, "version.txt");
        int version = -1;
        if (File.Exists(legacyVersionFilePath))
        {
            try
            {
                string versionText = File.ReadAllText(legacyVersionFilePath);
                if (!int.TryParse(versionText, out version))
                {
                    version = -1;
                }
            }
            catch (Exception ex)
            {
                Logger.F(TAG, ex);
            }
        }

        if (version < 0)
        {
            // New app
            version = DatabaseVersionModel.MainVersion;
        }

        if (version == DatabaseVersionModel.MAIN_VERSION)
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
                goto case 2;
            case 2: // 3.1.0
                if (EnvironmentProvider.IsPortable())
                {
                    string userFolder = Directory.GetParent(StorageLocation.LocalFolderPath)!.FullName;
                    MergeToDirectory(
                        Path.Combine(userFolder, "local_cache"),
                        StorageLocation.LocalCacheFolderPath);
                }

                goto case 3;
            case 3: // 3.4.0
                {
                    string oldConfigPath = Path.Combine(StorageLocation.LocalFolderPath, "configs", "1.database_version.json");
                    if (File.Exists(oldConfigPath))
                    {
                        File.Move(
                            oldConfigPath,
                            Path.Join(StorageLocation.LocalFolderPath, "Versions.json"),
                            overwrite: true);
                    }

                    if (File.Exists(legacyVersionFilePath))
                    {
                        File.Delete(legacyVersionFilePath);
                    }

                    MergeToDirectory(
                        Path.Combine(StorageLocation.LocalCacheFolderPath, "image_cache"),
                        Path.Combine(StorageLocation.LocalCacheFolderPath, "ImageCache"));
                }
                break;
            default:
                break;
        }

        DatabaseVersionModel.MainVersion = DatabaseVersionModel.MAIN_VERSION;
    }

    public static void UpgradeDatabaseAfterInitialization()
    {
        UpgradeVersionModel();
        UpgradeKVStore();
        UpgradeSqliteDatabase();
        UpgradeFavorites();
        UpgradeHistory();
        UpgradeAppSettings();
    }

    private static void UpgradeVersionModel()
    {
        if (DatabaseVersionModel.Version >= DatabaseVersionModel.VERSION)
        {
            return;
        }

        DatabaseVersionModel.Version = DatabaseVersionModel.VERSION;
    }

    private static void UpgradeKVStore()
    {
        if (DatabaseVersionModel.KVStoreVersion >= DatabaseVersionModel.KV_STORE_VERSION)
        {
            return;
        }

        switch (DatabaseVersionModel.KVStoreVersion)
        {
            case 0: // 2.8.1
                AppDB.AppKV.GetCollection(KVNames.KV_LIB_TIPS).Set(KVNames.KV_KEY_TIPS_READER_TIP_SHOWN, false);
                break;
            default:
                break;
        }

        DatabaseVersionModel.KVStoreVersion = DatabaseVersionModel.KV_STORE_VERSION;
    }

    private static void UpgradeSqliteDatabase()
    {
        if (DatabaseVersionModel.SqliteDatabaseVersion >= DatabaseVersionModel.SQLITE_DATABASE_VERSION)
        {
            return;
        }

        SqliteDB.UpdateDatabase(DatabaseVersionModel.SqliteDatabaseVersion);
        DatabaseVersionModel.SqliteDatabaseVersion = DatabaseVersionModel.SQLITE_DATABASE_VERSION;
    }

    private static void UpgradeFavorites()
    {
        if (DatabaseVersionModel.FavoritesVersion >= DatabaseVersionModel.FAVORITES_VERSION)
        {
            return;
        }

        DatabaseVersionModel.FavoritesVersion = DatabaseVersionModel.FAVORITES_VERSION;
    }

    private static void UpgradeHistory()
    {
        if (DatabaseVersionModel.HistoryVersion >= DatabaseVersionModel.HISTORY_VERSION)
        {
            return;
        }

        DatabaseVersionModel.HistoryVersion = DatabaseVersionModel.HISTORY_VERSION;
    }

    private static void UpgradeAppSettings()
    {
        if (DatabaseVersionModel.AppSettingsVersion >= DatabaseVersionModel.APP_SETTING_VERSION)
        {
            return;
        }

        DatabaseVersionModel.AppSettingsVersion = DatabaseVersionModel.APP_SETTING_VERSION;
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
