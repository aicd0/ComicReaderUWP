// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;

using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Core.Common.AppEnvironment;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Storage;
using ComicReaderUWP.Data.Models.Misc;

namespace ComicReaderUWP.Data.Database;

internal static class DatabaseUpgradeManager
{
    private const string TAG = nameof(DatabaseUpgradeManager);
    private const int VERSION = 3;

    private static string VersionFilePath => Path.Combine(StorageLocation.LocalFolderPath, "version.txt");

    public static void UpgradeDatabaseBeforeInitialization()
    {
        string versionFile = VersionFilePath;
        FileStream stream;
        try
        {
            stream = new FileStream(versionFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to open or create version file exclusively: {versionFile}", ex);
        }

        using (stream)
        {
            int version = -1;
            try
            {
                if (stream.Length > 0)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    using var sr = new StreamReader(stream, leaveOpen: true);
                    string content = sr.ReadToEnd();
                    if (!int.TryParse(content, out version))
                    {
                        version = -1;
                    }
                }
                else
                {
                    version = -1;
                }
            }
            catch (Exception ex)
            {
                Logger.E(TAG, ex);
                version = -1;
            }

            if (version < 0)
            {
                // New app
                stream.SetLength(0);
                stream.Seek(0, SeekOrigin.Begin);
                using (var sw = new StreamWriter(stream, leaveOpen: true))
                {
                    sw.Write(VERSION.ToString());
                    sw.Flush();
                }

                version = VERSION;
            }

            if (version == VERSION)
            {
                return;
            }

            UpgradeDatabaseBeforeInitializationInternal(version);

            stream.SetLength(0);
            stream.Seek(0, SeekOrigin.Begin);
            using (var sw = new StreamWriter(stream, leaveOpen: true))
            {
                sw.Write(VERSION.ToString());
                sw.Flush();
            }
        }
    }

    public static void UpgradeDatabaseAfterInitialization()
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

    private static void UpgradeDatabaseBeforeInitializationInternal(int version)
    {
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

                break;
            default:
                break;
        }
    }

    private static bool UpgradeVersionModel(DatabaseVersionModel.ExternalModel versions)
    {
        if (versions.Version >= DatabaseVersionModel.VERSION)
        {
            return false;
        }

        versions.Version = DatabaseVersionModel.VERSION;
        return true;
    }

    private static bool UpgradeKVStore(DatabaseVersionModel.ExternalModel versions)
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

    private static bool UpgradeSqliteDatabase(DatabaseVersionModel.ExternalModel versions)
    {
        if (versions.SqliteDatabaseVersion >= DatabaseVersionModel.SQLITE_DATABASE_VERSION)
        {
            return false;
        }

        SqliteDB.UpdateDatabase(versions.SqliteDatabaseVersion);
        versions.SqliteDatabaseVersion = DatabaseVersionModel.SQLITE_DATABASE_VERSION;
        return true;
    }

    private static bool UpgradeFavorites(DatabaseVersionModel.ExternalModel versions)
    {
        if (versions.FavoritesVersion >= DatabaseVersionModel.FAVORITES_VERSION)
        {
            return false;
        }

        versions.FavoritesVersion = DatabaseVersionModel.FAVORITES_VERSION;
        return true;
    }

    private static bool UpgradeHistory(DatabaseVersionModel.ExternalModel versions)
    {
        if (versions.HistoryVersion >= DatabaseVersionModel.HISTORY_VERSION)
        {
            return false;
        }

        versions.HistoryVersion = DatabaseVersionModel.HISTORY_VERSION;
        return true;
    }

    private static bool UpgradeAppSettings(DatabaseVersionModel.ExternalModel versions)
    {
        if (versions.AppSettingsVersion >= DatabaseVersionModel.APP_SETTING_VERSION)
        {
            return false;
        }

        versions.AppSettingsVersion = DatabaseVersionModel.APP_SETTING_VERSION;
        return true;
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
