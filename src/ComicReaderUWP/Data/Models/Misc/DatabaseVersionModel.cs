// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using ComicReaderUWP.Core.Database.Misc;
using ComicReaderUWP.Data.Database;

namespace ComicReaderUWP.Data.Models.Misc;

class DatabaseVersionModel : JsonDatabase<DatabaseVersionModel.JsonModel>
{
    public const int VERSION = 1;
    public const int SQLITE_DATABASE_VERSION = SqliteDB.DATABASE_VERSION;
    public const int KV_STORE_VERSION = 1;
    public const int FAVORITES_VERSION = 1;
    public const int HISTORY_VERSION = 1;
    public const int APP_SETTING_VERSION = 1;

    public static readonly DatabaseVersionModel Instance = new();

    private DatabaseVersionModel() : base("database_version.json") { }

    protected override JsonModel InitializeModel(JsonModel? model)
    {
        if (model is null)
        {
            return new()
            {
                Version = VERSION,
                KVStoreVersion = KV_STORE_VERSION,
                SqliteDatabaseVersion = SQLITE_DATABASE_VERSION,
                FavoritesVersion = FAVORITES_VERSION,
                HistoryVersion = HISTORY_VERSION,
                AppSettingsVersion = APP_SETTING_VERSION,
            };
        }

        return model;
    }

    public ExternalModel GetModel()
    {
        return Read(ExternalModel.FromJsonModel);
    }

    public void UpdateModel(ExternalModel model)
    {
        Write(model.ToJsonModel);
        Save();
    }

    public class JsonModel
    {
        [JsonPropertyName("Version")]
        public int? Version { get; set; }

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

    public class ExternalModel
    {
        public int Version { get; set; }
        public int KVStoreVersion { get; set; }
        public int SqliteDatabaseVersion { get; set; }
        public int FavoritesVersion { get; set; }
        public int HistoryVersion { get; set; }
        public int AppSettingsVersion { get; set; }

        public static ExternalModel FromJsonModel(JsonModel model)
        {
            return new ExternalModel
            {
                Version = model.Version ?? model.LegacyVersion ?? 0,
                KVStoreVersion = model.KVStoreVersion ?? 0,
                SqliteDatabaseVersion = model.SqliteDatabaseVersion ?? model.LegacySqliteDatabaseVersion ?? 0,
                FavoritesVersion = model.FavoritesVersion ?? model.LegacyFavoritesVersion ?? 0,
                HistoryVersion = model.HistoryVersion ?? model.LegacyHistoryVersion ?? 0,
                AppSettingsVersion = model.AppSettingsVersion ?? model.LegacyAppSettingsVersion ?? 0,
            };
        }

        public void ToJsonModel(JsonModel model)
        {
            model.Version = Version;
            model.KVStoreVersion = KVStoreVersion;
            model.SqliteDatabaseVersion = SqliteDatabaseVersion;
            model.FavoritesVersion = FavoritesVersion;
            model.HistoryVersion = HistoryVersion;
            model.AppSettingsVersion = AppSettingsVersion;
        }
    }
}
