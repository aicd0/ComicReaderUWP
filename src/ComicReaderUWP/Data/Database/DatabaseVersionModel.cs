// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Text.Json.Serialization;

using ComicReaderUWP.Core.Common.Storage;
using ComicReaderUWP.Core.Database.JSON;

namespace ComicReaderUWP.Data.Database;

internal static class DatabaseVersionModel
{
    public const int VERSION = 1;
    public const int MAIN_VERSION = 4;
    public const int SQLITE_DATABASE_VERSION = SqliteDB.DATABASE_VERSION;
    public const int KV_STORE_VERSION = 1;
    public const int FAVORITES_VERSION = 1;
    public const int HISTORY_VERSION = 1;
    public const int APP_SETTING_VERSION = 1;

    private static readonly string _versionFilePath = Path.Combine(StorageLocation.LocalFolderPath, "Versions.json");
    private static readonly JsonDatabase _db = new();

    public static int Version
    {
        get
        {
            return _db.Read(model => model.Version ?? model.LegacyVersion ?? 0);
        }
        set
        {
            _db.Write(model => model.Version = value);
            _db.Save();
        }
    }

    public static int MainVersion
    {
        get
        {
            return _db.Read(model => model.MainVersion ?? 0);
        }
        set
        {
            _db.Write(model => model.MainVersion = value);
            _db.Save();
        }
    }

    public static int KVStoreVersion
    {
        get
        {
            return _db.Read(model => model.KVStoreVersion ?? 0);
        }
        set
        {
            _db.Write(model => model.KVStoreVersion = value);
            _db.Save();
        }
    }

    public static int SqliteDatabaseVersion
    {
        get
        {
            return _db.Read(model => model.SqliteDatabaseVersion ?? model.LegacySqliteDatabaseVersion ?? 0);
        }
        set
        {
            _db.Write(model => model.SqliteDatabaseVersion = value);
            _db.Save();
        }
    }

    public static int FavoritesVersion
    {
        get
        {
            return _db.Read(model => model.FavoritesVersion ?? model.LegacyFavoritesVersion ?? 0);
        }
        set
        {
            _db.Write(model => model.FavoritesVersion = value);
            _db.Save();
        }
    }

    public static int HistoryVersion
    {
        get
        {
            return _db.Read(model => model.HistoryVersion ?? model.LegacyHistoryVersion ?? 0);
        }
        set
        {
            _db.Write(model => model.HistoryVersion = value);
            _db.Save();
        }
    }

    public static int AppSettingsVersion
    {
        get
        {
            return _db.Read(model => model.AppSettingsVersion ?? model.LegacyAppSettingsVersion ?? 0);
        }
        set
        {
            _db.Write(model => model.AppSettingsVersion = value);
            _db.Save();
        }
    }

    private class JsonDatabase : JsonDatabase<JsonModel>
    {
        public JsonDatabase() : base(new FileLayer(_versionFilePath)) { }

        protected override JsonModel InitializeModel(JsonModel? model)
        {
            if (model is null)
            {
                return new()
                {
                    Version = VERSION,
                    MainVersion = MAIN_VERSION,
                    KVStoreVersion = KV_STORE_VERSION,
                    SqliteDatabaseVersion = SQLITE_DATABASE_VERSION,
                    FavoritesVersion = FAVORITES_VERSION,
                    HistoryVersion = HISTORY_VERSION,
                    AppSettingsVersion = APP_SETTING_VERSION,
                };
            }

            return model;
        }
    }

    public class JsonModel
    {
        [JsonPropertyName("Version")]
        public int? Version { get; set; }

        [JsonPropertyName("MainVersion")]
        public int? MainVersion { get; set; }

        [JsonPropertyName("KVStoreVersion")]
        public int? KVStoreVersion { get; set; }

        [JsonPropertyName("SqliteDatabaseVersion")]
        public int? SqliteDatabaseVersion { get; set; }

        [JsonPropertyName("FavoritesVersion")]
        public int? FavoritesVersion { get; set; }

        [JsonPropertyName("HistoryVersion")]
        public int? HistoryVersion { get; set; }

        [JsonPropertyName("AppSettingsVersion")]
        public int? AppSettingsVersion { get; set; }

        //
        // Legacy
        //

        [JsonPropertyName("database_versions_version")]
        public int? LegacyVersion { get; set; }

        [JsonPropertyName("comic_database_version")]
        public int? LegacySqliteDatabaseVersion { get; set; }

        [JsonPropertyName("favorites_version")]
        public int? LegacyFavoritesVersion { get; set; }

        [JsonPropertyName("history_version")]
        public int? LegacyHistoryVersion { get; set; }

        [JsonPropertyName("app_setting_version")]
        public int? LegacyAppSettingsVersion { get; set; }
    }
}
