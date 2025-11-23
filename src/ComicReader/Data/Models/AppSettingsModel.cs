// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using ComicReader.Common.Utils;
using ComicReader.SDK.Data;

namespace ComicReader.Data.Models;

public class AppSettingsModel : JsonDatabase<AppSettingsModel.JsonModel>
{
    private const string APP_BACKGROUND_NONE = "None";
    private const string APP_BACKGROUND_ACRYLIC = "Acrylic";

    public static readonly AppSettingsModel Instance = new();

    private AppSettingsModel() : base("settings.json") { }

    protected override JsonModel CreateModel()
    {
        return new();
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

    public void UpdateModel(ExternalModel model)
    {
        Write(m =>
        {
            model.To(m);
            return true;
        });
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
    // Types
    //

    public class JsonModel
    {
        [JsonPropertyName("ComicFolders")]
        public List<string?>? ComicFolders { get; set; }

        [JsonPropertyName("RemoveUnreachableComics")]
        public bool? RemoveUnreachableComics { get; set; }

        [JsonPropertyName("PromptBeforeRemovingComics")]
        public bool? PromptBeforeRemovingComics { get; set; }

        [JsonPropertyName("RestoreLastReadingPosition")]
        public bool? RestoreLastReadingPosition { get; set; }

        [JsonPropertyName("Language")]
        public string? Language { get; set; }

        [JsonPropertyName("Theme")]
        public int? Theme { get; set; }

        [JsonPropertyName("Background")]
        public string? Background { get; set; }

        [JsonPropertyName("ReaderSettingPresets")]
        public Dictionary<string, ReaderSettingJsonModel?>? ReaderSettingPresets { get; set; }

        [JsonPropertyName("DefaultReaderSettingPresetKey")]
        public string? DefaultReaderSettingPresetKey { get; set; }

        [JsonPropertyName("ComicShuffleRandomSeed")]
        public int? ComicShuffleRandomSeed { get; set; }
    }

    public class ReaderSettingJsonModel
    {
        [JsonPropertyName("PresetName")]
        public string? PresetName { get; set; }

        [JsonPropertyName("OriginalSize")]
        public bool? OriginalSize { get; set; }

        [JsonPropertyName("VerticalReading")]
        public bool? VerticalReading { get; set; }

        [JsonPropertyName("LeftToRight")]
        public bool? LeftToRight { get; set; }

        [JsonPropertyName("VerticalContinuous")]
        public bool? VerticalContinuous { get; set; }

        [JsonPropertyName("HorizontalContinuous")]
        public bool? HorizontalContinuous { get; set; }

        [JsonPropertyName("VerticalPageArrangement")]
        public int? VerticalPageArrangement { get; set; }

        [JsonPropertyName("HorizontalPageArrangement")]
        public int? HorizontalPageArrangement { get; set; }

        [JsonPropertyName("PageGap")]
        public int? PageGap { get; set; }

        [JsonPropertyName("AutoScrollSpeed")]
        public int? AutoScrollSpeed { get; set; }
    }

    public class ExternalModel
    {
        public List<string> ComicFolders { get; set; } = [];
        public bool RemoveUnreachableComics { get; set; }
        public bool PromptBeforeRemovingComics { get; set; }
        public bool RestoreLastReadingPosition { get; set; }
        public string Language { get; set; } = "";
        public AppearanceSetting Theme { get; set; } = AppearanceSetting.UseSystemSetting;
        public AppBackgroundEnum Background { get; set; } = AppBackgroundEnum.None;
        public Dictionary<string, ReaderSettingModel> ReaderSettingPresets { get; set; } = [];
        public string DefaultReaderSettingPresetKey { get; set; } = string.Empty;
        public int ComicShuffleRandomSeed { get; set; }

        public static ExternalModel From(JsonModel model)
        {
            ExternalModel externalModel = new()
            {
                RemoveUnreachableComics = model.RemoveUnreachableComics ?? true,
                PromptBeforeRemovingComics = model.PromptBeforeRemovingComics ?? true,
                RestoreLastReadingPosition = model.RestoreLastReadingPosition ?? true,
                Language = model.Language ?? "",
                ComicShuffleRandomSeed = model.ComicShuffleRandomSeed ?? 0,
                DefaultReaderSettingPresetKey = model.DefaultReaderSettingPresetKey ?? string.Empty,
            };

            if (model.ReaderSettingPresets is not null)
            {
                foreach (KeyValuePair<string, ReaderSettingJsonModel?> kvp in model.ReaderSettingPresets)
                {
                    string key = kvp.Key;
                    ReaderSettingJsonModel? settingJsonModel = kvp.Value;
                    if (settingJsonModel is not null)
                    {
                        externalModel.ReaderSettingPresets[key] = ReaderSettingModel.From(settingJsonModel);
                    }
                }
            }

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
            model.RemoveUnreachableComics = RemoveUnreachableComics;
            model.RestoreLastReadingPosition = RestoreLastReadingPosition;
            model.PromptBeforeRemovingComics = PromptBeforeRemovingComics;
            model.Language = Language;
            model.Theme = (int)Theme;
            model.ComicShuffleRandomSeed = ComicShuffleRandomSeed;
            model.DefaultReaderSettingPresetKey = DefaultReaderSettingPresetKey;

            model.Background = Background switch
            {
                AppBackgroundEnum.Acrylic => APP_BACKGROUND_ACRYLIC,
                _ => APP_BACKGROUND_NONE,
            };

            model.ReaderSettingPresets = [];
            foreach (KeyValuePair<string, ReaderSettingModel> kvp in ReaderSettingPresets)
            {
                string key = kvp.Key;
                ReaderSettingModel settingModel = kvp.Value;
                model.ReaderSettingPresets[key] = settingModel.To();
            }
        }
    }

    public class ReaderSettingModel
    {
        public string PresetName { get; set; } = string.Empty;
        public bool OriginalSize { get; set; }
        public bool VerticalReading { get; set; }
        public bool LeftToRight { get; set; }
        public bool VerticalContinuous { get; set; }
        public bool HorizontalContinuous { get; set; }
        public PageArrangementEnum VerticalPageArrangement { get; set; }
        public PageArrangementEnum HorizontalPageArrangement { get; set; }
        public int PageGap { get; set; }
        public int AutoScrollSpeed { get; set; }

        public static ReaderSettingModel From(ReaderSettingJsonModel model)
        {
            return new ReaderSettingModel
            {
                PresetName = model.PresetName ?? "?",
                OriginalSize = model.OriginalSize ?? false,
                VerticalReading = model.VerticalReading ?? true,
                LeftToRight = model.LeftToRight ?? false,
                VerticalContinuous = model.VerticalContinuous ?? true,
                HorizontalContinuous = model.HorizontalContinuous ?? false,
                VerticalPageArrangement = ParsePageArrangementEnum(model.VerticalPageArrangement) ?? PageArrangementEnum.Single,
                HorizontalPageArrangement = ParsePageArrangementEnum(model.HorizontalPageArrangement) ?? PageArrangementEnum.DualCoverMirror,
                PageGap = model.PageGap ?? 100,
                AutoScrollSpeed = model.AutoScrollSpeed ?? 0,
            };
        }

        public ReaderSettingJsonModel To()
        {
            return new()
            {
                PresetName = PresetName,
                OriginalSize = OriginalSize,
                VerticalReading = VerticalReading,
                LeftToRight = LeftToRight,
                VerticalContinuous = VerticalContinuous,
                HorizontalContinuous = HorizontalContinuous,
                VerticalPageArrangement = (int)VerticalPageArrangement,
                HorizontalPageArrangement = (int)HorizontalPageArrangement,
                PageGap = PageGap,
                AutoScrollSpeed = AutoScrollSpeed,
            };
        }

        private static PageArrangementEnum? ParsePageArrangementEnum(int? value)
        {
            if (value.HasValue && Enum.IsDefined(typeof(PageArrangementEnum), value))
            {
                return (PageArrangementEnum)value;
            }
            return null;
        }
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
}
