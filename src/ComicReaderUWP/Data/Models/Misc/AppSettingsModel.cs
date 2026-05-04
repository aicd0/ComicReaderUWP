// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.SDK.Common.AppEnvironment;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Lifecycle;
using ComicReaderUWP.SDK.Common.Utils;
using ComicReaderUWP.SDK.Database.Misc;
using ComicReaderUWP.Views.Pages.Main;

using Windows.Globalization;

namespace ComicReaderUWP.Data.Models.Misc;

internal class AppSettingsModel : JsonDatabase<AppSettingsModel.JsonModel>
{
    private const string TAG = nameof(AppSettingsModel);
    private const string APP_BACKGROUND_NONE = "None";
    private const string APP_BACKGROUND_ACRYLIC = "Acrylic";

    public static readonly AppSettingsModel Instance = new();

    //
    // Events
    //

    private readonly MutableLiveData<bool> _keepScreenOnBehaviorChangeLiveData = new();
    public ILiveData<bool> KeepScreenOnBehaviorChangedLiveData => _keepScreenOnBehaviorChangeLiveData;

    //
    // Properties
    //

    public bool AutomaticallyHideCursor
    {
        get
        {
            return Read(model => model.AutomaticallyHideCursor ?? false);
        }
        set
        {
            Write(model => model.AutomaticallyHideCursor = value);
            Save();
        }
    }

    public bool AutoSwitch
    {
        get
        {
            return Read(model => model.AutoSwitch ?? false);
        }
        set
        {
            Write(model => model.AutoSwitch = value);
            Save();
        }
    }

    public CloseLastTabBehaviorEnum CloseLastTabBehavior
    {
        get
        {
            return Read(model => ConvertCloseLastTabBehaviorFromJson(model.CloseLastTabBehavior));
        }
        set
        {
            Write(model => model.CloseLastTabBehavior = ConvertCloseLastTabBehaviorToJson(value));
            Save();
        }
    }

    public int DefaultArchiveCodePage
    {
        get
        {
            return Read(model => model.DefaultArchiveCodePage ?? -1);
        }
        set
        {
            Write(model => model.DefaultArchiveCodePage = value);
            Save();
        }
    }

    public KeepScreenOnBehaviorEnum KeepScreenOnBehavior
    {
        get
        {
            return Read(model => ConvertKeepScreenOnBehaviorFromJson(model.KeepScreenOnBehavior));
        }
        set
        {
            Write(model => model.KeepScreenOnBehavior = ConvertKeepScreenOnBehaviorToJson(value));
            Save();
            _keepScreenOnBehaviorChangeLiveData.Emit(true);
        }
    }

    public string Language
    {
        get
        {
            return Read(model => model.Language ?? string.Empty);
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

            Write(model => model.Language = value);
            Save();
        }
    }

    public OpenComicBehaviorEnum OpenComicDefaultBehavior
    {
        get
        {
            return Read(model => ConvertOpenComicDefaultBehaviorFromJson(model.OpenComicDefaultBehavior));
        }
        set
        {
            Write(model => model.OpenComicDefaultBehavior = ConvertOpenComicDefaultBehaviorToJson(value));
            Save();
        }
    }

    public bool PlaybackDefaultRepeat
    {
        get
        {
            return Read(model => model.PlaybackDefaultRepeat ?? false);
        }
        set
        {
            Write(model => model.PlaybackDefaultRepeat = value);
            Save();
        }
    }

    public bool PlaybackDefaultShuffle
    {
        get
        {
            return Read(model => model.PlaybackDefaultShuffle ?? false);
        }
        set
        {
            Write(model => model.PlaybackDefaultShuffle = value);
            Save();
        }
    }

    public bool RatingPercentageEnabled
    {
        get
        {
            return Read(model => model.RatingPercentageEnabled ?? false);
        }
        set
        {
            Write(model => model.RatingPercentageEnabled = value);
            Save();
        }
    }

    public bool RestoreLastReadingPosition
    {
        get
        {
            return Read(model => model.RestoreLastReadingPosition ?? true);
        }
        set
        {
            Write(model => model.RestoreLastReadingPosition = value);
            Save();
        }
    }

    public bool RestoreLastReadingPositionOnlyAppliesToReadingComics
    {
        get
        {
            return Read(model => model.RestoreLastReadingPositionOnlyAppliesToReadingComics ?? false);
        }
        set
        {
            Write(model => model.RestoreLastReadingPositionOnlyAppliesToReadingComics = value);
            Save();
        }
    }

    public bool UseScrollingAreaAsStartEnd
    {
        get
        {
            return Read(model => model.UseScrollingAreaAsStartEnd ?? false);
        }
        set
        {
            Write(model => model.UseScrollingAreaAsStartEnd = value);
            Save();
        }
    }

    public bool SaveBrowsingHistory
    {
        get
        {
            return Read(model => model.SaveBrowsingHistory ?? true);
        }
        set
        {
            Write(model => model.SaveBrowsingHistory = value);
            Save();
        }
    }

    public bool TransitionAnimation
    {
        get
        {
            return Read(model => model.TransitionAnimation ?? true);
        }
        set
        {
            Write(model => model.TransitionAnimation = value);
            Save();
        }
    }

    public string DefaultReaderSettingPresetKey
    {
        get
        {
            return Read(model => model.DefaultReaderSettingPresetKey ?? string.Empty);
        }
        set
        {
            Write(model => model.DefaultReaderSettingPresetKey = value);
            Save();
        }
    }

    public Dictionary<string, ReaderSettingsModel> ReaderSettingPresets
    {
        get
        {
            return Read(model =>
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
            Write(model =>
            {
                model.ReaderSettingPresets = [];
                foreach (KeyValuePair<string, ReaderSettingsModel> kvp in value)
                {
                    string key = kvp.Key;
                    ReaderSettingsModel setting = kvp.Value;
                    model.ReaderSettingPresets[key] = setting.ToJsonModel();
                }
            });
            Save();
        }
    }

    //
    // Constructor
    //

    private AppSettingsModel() : base("settings.json") { }

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

    //
    // Getters
    //

    public ExternalModel GetModel()
    {
        return Read(ExternalModel.From);
    }

    //
    // Setters
    //

    public void Reset()
    {
        JsonModel newModel = new();
        Read(model =>
        {
            newModel.ComicFolders = model.ComicFolders;
        });

        Write(newModel);
        Language = Language; // Language config needs to be applied immediately to take effect on next launch
    }

    public void UpdateModel(ExternalModel model)
    {
        Write(model.To);
        Save();
    }

    public void AddComicFolder(string folderPath)
    {
        bool updated = Write(m =>
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
            Save();
        }
    }

    public void RemoveComicFolder(string folderPath)
    {
        bool updated = Write(m =>
        {
            m.ComicFolders ??= [];
            return m.ComicFolders.Remove(folderPath);
        });

        if (updated)
        {
            Save();
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
        [JsonPropertyName("AutomaticallyHideCursor")]
        public bool? AutomaticallyHideCursor { get; set; }

        [JsonPropertyName("AutoSwitch")]
        public bool? AutoSwitch { get; set; }

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
