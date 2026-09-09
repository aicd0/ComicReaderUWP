// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.AppEnvironment;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Core.Database.JSON;
using ComicReaderUWP.Data.Database;

using Windows.Globalization;

namespace ComicReaderUWP.Data.Models.Misc;

internal static class AppSettingsModel
{
    private const string TAG = nameof(AppSettingsModel);
    private const string APP_BACKGROUND_NONE = "None";
    private const string APP_BACKGROUND_ACRYLIC = "Acrylic";

    private static readonly JsonDatabase _db = new();

    //
    // Events
    //

    private static readonly MutableLiveData<bool> _keepScreenOnBehaviorChangeLiveData = new();
    public static ILiveData<bool> KeepScreenOnBehaviorChangedLiveData => _keepScreenOnBehaviorChangeLiveData;

    //
    // Properties
    //

    public static bool AutoHideCursor
    {
        get
        {
            return _db.Read(model => model.AutomaticallyHideCursor ?? false);
        }
        set
        {
            _db.Write(model => model.AutomaticallyHideCursor = value);
            _db.Save();
        }
    }

    public static bool AutoSwitch
    {
        get
        {
            return _db.Read(model => model.AutoSwitch ?? true);
        }
        set
        {
            _db.Write(model => model.AutoSwitch = value);
            _db.Save();
        }
    }

    public static bool AutoToggleOverlaysOnCursor
    {
        get
        {
            return _db.Read(model => model.AutoToggleOverlaysOnCursor ?? true);
        }
        set
        {
            _db.Write(model => model.AutoToggleOverlaysOnCursor = value);
            _db.Save();
        }
    }

    public static CloseLastTabBehaviorEnum CloseLastTabBehavior
    {
        get
        {
            return _db.Read(model => ConvertCloseLastTabBehaviorFromJson(model.CloseLastTabBehavior));
        }
        set
        {
            _db.Write(model => model.CloseLastTabBehavior = ConvertCloseLastTabBehaviorToJson(value));
            _db.Save();
        }
    }

    public static int DefaultArchiveCodePage
    {
        get
        {
            return _db.Read(model => model.DefaultArchiveCodePage ?? -1);
        }
        set
        {
            _db.Write(model => model.DefaultArchiveCodePage = value);
            _db.Save();
        }
    }

    public static KeepScreenOnBehaviorEnum KeepScreenOnBehavior
    {
        get
        {
            return _db.Read(model => ConvertKeepScreenOnBehaviorFromJson(model.KeepScreenOnBehavior));
        }
        set
        {
            _db.Write(model => model.KeepScreenOnBehavior = ConvertKeepScreenOnBehaviorToJson(value));
            _db.Save();
            _keepScreenOnBehaviorChangeLiveData.Emit(true);
        }
    }

    public static string Language
    {
        get
        {
            return _db.Read(model => model.Language ?? string.Empty);
        }
        set
        {
            if (!EnvironmentProvider.IsPortable())
            {
                try
                {
                    ApplicationLanguages.PrimaryLanguageOverride = value;
                }
                catch (Exception ex)
                {
                    Logger.F(TAG, ex);
                }
            }

            _db.Write(model => model.Language = value);
            _db.Save();
        }
    }

    public static OpenComicBehaviorEnum OpenComicDefaultBehavior
    {
        get
        {
            return _db.Read(model => ConvertOpenComicDefaultBehaviorFromJson(model.OpenComicDefaultBehavior));
        }
        set
        {
            _db.Write(model => model.OpenComicDefaultBehavior = ConvertOpenComicDefaultBehaviorToJson(value));
            _db.Save();
        }
    }

    public static bool PlaybackDefaultRepeat
    {
        get
        {
            return _db.Read(model => model.PlaybackDefaultRepeat ?? false);
        }
        set
        {
            _db.Write(model => model.PlaybackDefaultRepeat = value);
            _db.Save();
        }
    }

    public static bool PlaybackDefaultShuffle
    {
        get
        {
            return _db.Read(model => model.PlaybackDefaultShuffle ?? false);
        }
        set
        {
            _db.Write(model => model.PlaybackDefaultShuffle = value);
            _db.Save();
        }
    }

    public static int PreloadPagesAfter
    {
        get
        {
            return _db.Read(model => model.PreloadPagesAfter ?? 5);
        }
        set
        {
            _db.Write(model => model.PreloadPagesAfter = value);
            _db.Save();
        }
    }

    public static int PreloadPagesBefore
    {
        get
        {
            return _db.Read(model => model.PreloadPagesBefore ?? 5);
        }
        set
        {
            _db.Write(model => model.PreloadPagesBefore = value);
            _db.Save();
        }
    }

    public static bool RatingPercentageEnabled
    {
        get
        {
            return _db.Read(model => model.RatingPercentageEnabled ?? false);
        }
        set
        {
            _db.Write(model => model.RatingPercentageEnabled = value);
            _db.Save();
        }
    }

    public static bool RestoreLastReadingPosition
    {
        get
        {
            return _db.Read(model => model.RestoreLastReadingPosition ?? true);
        }
        set
        {
            _db.Write(model => model.RestoreLastReadingPosition = value);
            _db.Save();
        }
    }

    public static bool RestoreLastReadingPositionOnlyAppliesToReadingComics
    {
        get
        {
            return _db.Read(model => model.RestoreLastReadingPositionOnlyAppliesToReadingComics ?? false);
        }
        set
        {
            _db.Write(model => model.RestoreLastReadingPositionOnlyAppliesToReadingComics = value);
            _db.Save();
        }
    }

    public static bool SendUsageData
    {
        get
        {
            return _db.Read(model => model.SendUsageData ?? true);
        }
        set
        {
            _db.Write(model => model.SendUsageData = value);
            _db.Save();
        }
    }

    public static bool UseScrollingAreaAsStartEnd
    {
        get
        {
            return _db.Read(model => model.UseScrollingAreaAsStartEnd ?? false);
        }
        set
        {
            _db.Write(model => model.UseScrollingAreaAsStartEnd = value);
            _db.Save();
        }
    }

    public static bool SaveBrowsingHistory
    {
        get
        {
            return _db.Read(model => model.SaveBrowsingHistory ?? true);
        }
        set
        {
            _db.Write(model => model.SaveBrowsingHistory = value);
            _db.Save();
        }
    }

    public static bool EnableCompressedFileCache
    {
        get
        {
            return _db.Read(model => model.EnableCompressedFileCache ?? true);
        }
        set
        {
            _db.Write(model => model.EnableCompressedFileCache = value);
            _db.Save();
        }
    }

    public static bool TransitionAnimation
    {
        get
        {
            return _db.Read(model => model.TransitionAnimation ?? true);
        }
        set
        {
            _db.Write(model => model.TransitionAnimation = value);
            _db.Save();
        }
    }

    public static string DefaultReaderSettingPresetKey
    {
        get
        {
            return _db.Read(model => model.DefaultReaderSettingPresetKey ?? string.Empty);
        }
        set
        {
            _db.Write(model => model.DefaultReaderSettingPresetKey = value);
            _db.Save();
        }
    }

    public static Dictionary<string, ReaderSettingsModel> ReaderSettingPresets
    {
        get
        {
            return _db.Read(model =>
            {
                Dictionary<string, ReaderSettingsModel> presets = [];
                if (model.ReaderSettingPresets is not null)
                {
                    foreach (KeyValuePair<string, ReaderSettingsModel.JsonModel?> kvp in model.ReaderSettingPresets)
                    {
                        string key = kvp.Key;
                        ReaderSettingsModel.JsonModel? settingJsonModel = kvp.Value;
                        if (settingJsonModel is not null)
                        {
                            presets[key] = ReaderSettingsModel.FromJsonModel(key, settingJsonModel);
                        }
                    }
                }

                return presets;
            });
        }
        set
        {
            _db.Write(model =>
            {
                model.ReaderSettingPresets = [];
                foreach (KeyValuePair<string, ReaderSettingsModel> kvp in value)
                {
                    string key = kvp.Key;
                    ReaderSettingsModel setting = kvp.Value;
                    model.ReaderSettingPresets[key] = setting.ToJsonModel();
                }
            });
            _db.Save();
        }
    }

    //
    // Getters
    //

    public static ExternalModel GetModel()
    {
        return _db.Read(ExternalModel.From);
    }

    //
    // Setters
    //

    public static void Reset()
    {
        JsonModel newModel = new();
        _db.Read(model =>
        {
            newModel.ComicFolders = model.ComicFolders;
        });

        _db.Write(newModel);
        Language = Language; // Language config needs to be applied immediately to take effect on next launch
    }

    public static void UpdateModel(ExternalModel model)
    {
        _db.Write(model.To);
        _db.Save();
    }

    public static void AddComicFolder(string folderPath)
    {
        bool updated = _db.Write(m =>
        {
            m.ComicFolders ??= [];
            for (int i = m.ComicFolders.Count - 1; i >= 0; i--)
            {
                string? oldPath = m.ComicFolders[i];
                if (string.IsNullOrEmpty(oldPath))
                {
                    m.ComicFolders.RemoveAt(i);
                    continue;
                }
                if (StringUtils.FolderContain(oldPath, folderPath))
                {
                    return false;
                }
                if (StringUtils.FolderContain(folderPath, oldPath))
                {
                    m.ComicFolders.RemoveAt(i);
                }
            }
            m.ComicFolders.Add(folderPath);
            return true;
        });

        if (updated)
        {
            _db.Save();
        }
    }

    public static void RemoveComicFolder(string folderPath)
    {
        bool updated = _db.Write(m =>
        {
            m.ComicFolders ??= [];
            return m.ComicFolders.Remove(folderPath);
        });

        if (updated)
        {
            _db.Save();
        }
    }

    //
    // Helpers
    //

    private static string ConvertCloseLastTabBehaviorToJson(CloseLastTabBehaviorEnum behavior)
    {
        return behavior switch
        {
            CloseLastTabBehaviorEnum.CloseWindow => "CloseWindow",
            CloseLastTabBehaviorEnum.OpenHomePage => "OpenHomePage",
            _ => "CloseWindow",
        };
    }

    private static CloseLastTabBehaviorEnum ConvertCloseLastTabBehaviorFromJson(string? behavior)
    {
        return behavior switch
        {
            "CloseWindow" => CloseLastTabBehaviorEnum.CloseWindow,
            "OpenHomePage" => CloseLastTabBehaviorEnum.OpenHomePage,
            _ => CloseLastTabBehaviorEnum.CloseWindow,
        };
    }

    private static string ConvertOpenComicDefaultBehaviorToJson(OpenComicBehaviorEnum behavior)
    {
        return behavior switch
        {
            OpenComicBehaviorEnum.OpenInCurrentTab => "OpenInCurrentTab",
            OpenComicBehaviorEnum.OpenInNewTab => "OpenInNewTab",
            OpenComicBehaviorEnum.OpenInLastActiveReaderTab => "OpenInLastActiveReaderTab",
            _ => "OpenInCurrentTab",
        };
    }

    private static OpenComicBehaviorEnum ConvertOpenComicDefaultBehaviorFromJson(string? behavior)
    {
        return behavior switch
        {
            "OpenInCurrentTab" => OpenComicBehaviorEnum.OpenInCurrentTab,
            "OpenInNewTab" => OpenComicBehaviorEnum.OpenInNewTab,
            "OpenInLastActiveReaderTab" => OpenComicBehaviorEnum.OpenInLastActiveReaderTab,
            _ => OpenComicBehaviorEnum.OpenInCurrentTab,
        };
    }

    private static string ConvertKeepScreenOnBehaviorToJson(KeepScreenOnBehaviorEnum behavior)
    {
        return behavior switch
        {
            KeepScreenOnBehaviorEnum.Never => "Never",
            KeepScreenOnBehaviorEnum.Always => "Always",
            KeepScreenOnBehaviorEnum.DuringAutoScrolling => "DuringAutoScrolling",
            _ => "DuringAutoScrolling",
        };
    }

    private static KeepScreenOnBehaviorEnum ConvertKeepScreenOnBehaviorFromJson(string? behavior)
    {
        return behavior switch
        {
            "Never" => KeepScreenOnBehaviorEnum.Never,
            "Always" => KeepScreenOnBehaviorEnum.Always,
            "DuringAutoScrolling" => KeepScreenOnBehaviorEnum.DuringAutoScrolling,
            _ => KeepScreenOnBehaviorEnum.DuringAutoScrolling,
        };
    }

    //
    // Types
    //

    private class JsonDatabase : JsonDatabase<JsonModel>
    {
        public JsonDatabase() : base(new SimpleConfigDatabaseLayer("settings.json")) { }

        protected override JsonModel InitializeModel(JsonModel? model)
        {
            model ??= new();
            model.OpenComicDefaultBehavior ??= model.HomePageTapComicBehavior;
            model.AutomaticallyHideCursor ??= AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).GetValueOrDefault(KVNames.KV_KEY_APP_AUTO_HIDE_CURSOR, false);
            model.DefaultArchiveCodePage ??= (int)AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).GetValueOrDefault<long>(KVNames.KV_KEY_APP_DEFAULT_ARCHIVE_CODE_PAGE, -1);
            model.RatingPercentageEnabled ??= AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).GetValueOrDefault(KVNames.KV_KEY_APP_RATING_PERCENTAGE_ENABLED, false);
            model.SaveBrowsingHistory ??= AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).GetValueOrDefault(KVNames.KV_KEY_APP_SAVE_BROWSING_HISTORY, true);
            model.TransitionAnimation ??= AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).GetValueOrDefault(KVNames.KV_KEY_APP_TRANSITION_ANIMATION, true);
            return model;
        }
    }

    public class ExternalModel
    {
        public List<string> ComicFolders { get; set; } = [];
        public bool ScanOnLaunch { get; set; }
        public bool RemoveUnreachableComics { get; set; }
        public bool PromptBeforeRemovingComics { get; set; }
        public AppearanceSetting Theme { get; set; } = AppearanceSetting.UseSystemSetting;
        public AppBackgroundEnum Background { get; set; } = AppBackgroundEnum.None;
        public int ComicShuffleRandomSeed { get; set; }

        public static ExternalModel From(JsonModel model)
        {
            ExternalModel externalModel = new()
            {
                ScanOnLaunch = model.ScanOnLaunch ?? true,
                RemoveUnreachableComics = model.RemoveUnreachableComics ?? true,
                PromptBeforeRemovingComics = model.PromptBeforeRemovingComics ?? true,
                ComicShuffleRandomSeed = model.ComicShuffleRandomSeed ?? 0,
            };

            if (model.ComicFolders is not null)
            {
                foreach (string? folder in model.ComicFolders)
                {
                    if (string.IsNullOrEmpty(folder))
                    {
                        continue;
                    }
                    externalModel.ComicFolders.Add(folder);
                }
            }

            int? theme = model.Theme;
            if (theme is null)
            {
                externalModel.Theme = AppearanceSetting.UseSystemSetting;
            }
            else if (Enum.IsDefined(typeof(AppearanceSetting), theme))
            {
                externalModel.Theme = (AppearanceSetting)theme;
            }
            else
            {
                externalModel.Theme = AppearanceSetting.UseSystemSetting;
            }

            externalModel.Background = model.Background switch
            {
                APP_BACKGROUND_ACRYLIC => AppBackgroundEnum.Acrylic,
                _ => AppBackgroundEnum.None,
            };

            return externalModel;
        }

        public void To(JsonModel model)
        {
            model.ComicFolders = [.. ComicFolders];
            model.ScanOnLaunch = ScanOnLaunch;
            model.RemoveUnreachableComics = RemoveUnreachableComics;
            model.PromptBeforeRemovingComics = PromptBeforeRemovingComics;
            model.Theme = (int)Theme;
            model.ComicShuffleRandomSeed = ComicShuffleRandomSeed;

            model.Background = Background switch
            {
                AppBackgroundEnum.Acrylic => APP_BACKGROUND_ACRYLIC,
                _ => APP_BACKGROUND_NONE,
            };
        }
    }

    public enum CloseLastTabBehaviorEnum
    {
        CloseWindow,
        OpenHomePage,
    }

    public enum OpenComicBehaviorEnum
    {
        OpenInCurrentTab,
        OpenInNewTab,
        OpenInLastActiveReaderTab,
    }

    public enum KeepScreenOnBehaviorEnum
    {
        Never,
        Always,
        DuringAutoScrolling,
    }

    public enum AppearanceSetting
    {
        Light,
        Dark,
        UseSystemSetting,
        None
    }

    public enum AppBackgroundEnum
    {
        None,
        Acrylic,
    }

    public class JsonModel
    {
        [JsonPropertyName("AutoSwitch")]
        public bool? AutoSwitch { get; set; }

        [JsonPropertyName("AutoToggleOverlaysOnCursor")]
        public bool? AutoToggleOverlaysOnCursor { get; set; }

        [JsonPropertyName("AutomaticallyHideCursor")]
        public bool? AutomaticallyHideCursor { get; set; }

        [JsonPropertyName("Background")]
        public string? Background { get; set; }

        [JsonPropertyName("CloseLastTabBehavior")]
        public string? CloseLastTabBehavior { get; set; }

        [JsonPropertyName("ComicFolders")]
        public List<string?>? ComicFolders { get; set; }

        [JsonPropertyName("ComicShuffleRandomSeed")]
        public int? ComicShuffleRandomSeed { get; set; }

        [JsonPropertyName("DefaultArchiveCodePage")]
        public int? DefaultArchiveCodePage { get; set; }

        [JsonPropertyName("DefaultReaderSettingPresetKey")]
        public string? DefaultReaderSettingPresetKey { get; set; }

        [JsonPropertyName("EnableCompressedFileCache")]
        public bool? EnableCompressedFileCache { get; set; }

        [JsonPropertyName("KeepScreenOnBehavior")]
        public string? KeepScreenOnBehavior { get; set; }

        [JsonPropertyName("OpenComicDefaultBehavior")]
        public string? OpenComicDefaultBehavior { get; set; }

        [JsonPropertyName("Language")]
        public string? Language { get; set; }

        [JsonPropertyName("PlaybackDefaultRepeat")]
        public bool? PlaybackDefaultRepeat { get; set; }

        [JsonPropertyName("PlaybackDefaultShuffle")]
        public bool? PlaybackDefaultShuffle { get; set; }

        [JsonPropertyName("PreloadPagesAfter")]
        public int? PreloadPagesAfter { get; set; }

        [JsonPropertyName("PreloadPagesBefore")]
        public int? PreloadPagesBefore { get; set; }

        [JsonPropertyName("PromptBeforeRemovingComics")]
        public bool? PromptBeforeRemovingComics { get; set; }

        [JsonPropertyName("RatingPercentageEnabled")]
        public bool? RatingPercentageEnabled { get; set; }

        [JsonPropertyName("ReaderSettingPresets")]
        public Dictionary<string, ReaderSettingsModel.JsonModel?>? ReaderSettingPresets { get; set; }

        [JsonPropertyName("RemoveUnreachableComics")]
        public bool? RemoveUnreachableComics { get; set; }

        [JsonPropertyName("RestoreLastReadingPosition")]
        public bool? RestoreLastReadingPosition { get; set; }

        [JsonPropertyName("RestoreLastReadingPositionOnlyAppliesToReadingComics")]
        public bool? RestoreLastReadingPositionOnlyAppliesToReadingComics { get; set; }

        [JsonPropertyName("SendUsageData")]
        public bool? SendUsageData { get; set; }

        [JsonPropertyName("UseScrollingAreaAsStartEnd")]
        public bool? UseScrollingAreaAsStartEnd { get; set; }

        [JsonPropertyName("SaveBrowsingHistory")]
        public bool? SaveBrowsingHistory { get; set; }

        [JsonPropertyName("ScanOnLaunch")]
        public bool? ScanOnLaunch { get; set; }

        [JsonPropertyName("Theme")]
        public int? Theme { get; set; }

        [JsonPropertyName("TransitionAnimation")]
        public bool? TransitionAnimation { get; set; }

        //
        // Legacy
        //

        [JsonPropertyName("HomePageTapComicBehavior")]
        public string? HomePageTapComicBehavior { get; set; }
    }
}
