// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;

namespace ComicReaderUWP.Views.Pages.Main;

internal class ReaderSettingsModel
{
    public const string PRESET_KEY_DEFAULT = "###default###";
    public const string PRESET_KEY_CUSTOM = "###custom###";
    private const string TAG = nameof(ReaderSettingsModel);

    public static ReaderSettingsModel FromDefault()
    {
        return new()
        {
            PresetKey = PRESET_KEY_DEFAULT,
            PresetName = StringResourceProvider.Instance.Default,
        };
    }

    public static ReaderSettingsModel FromJsonModel(string key, JsonModel? model)
    {
        ReaderSettingsModel defaultModel = new()
        {
            PresetKey = key,
        };

        if (model is null)
        {
            return defaultModel;
        }

        return new()
        {
            PresetKey = key,
            PresetName = model.PresetName ?? defaultModel.PresetName,
            OriginalSize = model.OriginalSize ?? defaultModel.OriginalSize,
            IsVertical = model.VerticalReading ?? defaultModel.IsVertical,
            IsLeftToRight = model.LeftToRight ?? defaultModel.IsLeftToRight,
            IsVerticalContinuous = model.VerticalContinuous ?? defaultModel.IsVerticalContinuous,
            IsHorizontalContinuous = model.HorizontalContinuous ?? defaultModel.IsHorizontalContinuous,
            VerticalPageLayout = model.VerticalPageLayout is null ?
                ParseLegacyPageLayout(model.LegacyVerticalPageArrangement) :
                PageLayoutSettings.FromJsonModel(model.VerticalPageLayout),
            HorizontalPageLayout = model.HorizontalPageLayout is null ?
                ParseLegacyPageLayout(model.LegacyHorizontalPageArrangement) :
                PageLayoutSettings.FromJsonModel(model.HorizontalPageLayout),
            PageSpacing = model.PageSpacing ?? defaultModel.PageSpacing,
            AutoScrollSpeed = model.AutoScrollSpeed ?? defaultModel.AutoScrollSpeed,
            ImageRotation = model.ImageRotation switch
            {
                "None" => ImageRotationEnum.None,
                "Rotate90" => ImageRotationEnum.Rotate90,
                "Rotate180" => ImageRotationEnum.Rotate180,
                "Rotate270" => ImageRotationEnum.Rotate270,
                _ => defaultModel.ImageRotation,
            },
            ImageFlip = model.ImageFlip ?? defaultModel.ImageFlip,
            ImageInvert = model.ImageInvert ?? defaultModel.ImageInvert,
            AntiAliasingFilter = model.AntiAliasingFilter ?? defaultModel.AntiAliasingFilter,
        };
    }

    public static ReaderSettingsModel LoadFromComic(ComicModel comic)
    {
        string defaultPresetKey = AppSettingsModel.Instance.DefaultReaderSettingPresetKey;
        if (string.IsNullOrEmpty(defaultPresetKey))
        {
            defaultPresetKey = PRESET_KEY_DEFAULT;
        }

        string? presetKey = comic.GetExt(ComicExt.READER_SETTING_PRESET_KEY);
        if (string.IsNullOrEmpty(presetKey))
        {
            presetKey = defaultPresetKey;
        }

        ReaderSettingsModel? presetModel = null;
        if (presetKey != PRESET_KEY_CUSTOM)
        {
            Dictionary<string, ReaderSettingsModel> presets = AppSettingsModel.Instance.ReaderSettingPresets;
            if (!presets.TryGetValue(presetKey, out presetModel))
            {
                if (!presets.TryGetValue(defaultPresetKey, out presetModel))
                {
                    foreach (KeyValuePair<string, ReaderSettingsModel> kvp in presets)
                    {
                        presetKey = kvp.Key;
                        presetModel = kvp.Value;
                        break;
                    }
                }
            }

            if (presetModel is null)
            {
                // Automatically import missing key, especially for default preset
                presetModel = FromDefault();
                presets[presetKey] = presetModel;
                AppSettingsModel.Instance.ReaderSettingPresets = presets;
            }
        }

        JsonModel? jsonModel = null;
        string? customSettingsJson = comic.GetExt(ComicExt.CUSTOM_READER_SETTINGS);
        if (!string.IsNullOrEmpty(customSettingsJson))
        {
            try
            {
                jsonModel = JsonSerializer.Deserialize<JsonModel>(customSettingsJson);
            }
            catch (JsonException ex)
            {
                Logger.E(TAG, ex);
            }
        }

        if (jsonModel is not null)
        {
            ReaderSettingsModel model = FromJsonModel(presetKey, jsonModel);
            if (presetModel is not null)
            {
                model.PresetName = presetModel.PresetName;
            }

            return model;
        }

        if (presetModel is not null)
        {
            return presetModel;
        }

        return FromDefault();
    }

    public static ReaderSettingsModel? LoadFromPreset(string presetKey)
    {
        if (presetKey == PRESET_KEY_CUSTOM)
        {
            return null;
        }

        Dictionary<string, ReaderSettingsModel> presets = AppSettingsModel.Instance.ReaderSettingPresets;
        if (!presets.TryGetValue(presetKey, out ReaderSettingsModel? presetModel))
        {
            return null;
        }

        return presetModel;
    }

    private static PageLayoutSettings ParseLegacyPageLayout(int? value)
    {
        PageLayoutSettings defaultModel = new();

        if (value is null)
        {
            return defaultModel;
        }

        return new()
        {
            TwoPageMode = value != 0,
            EnableCover = value <= 2,
            SwapLeftAndRightPages = value == 2 || value == 4,
            SpreadDetection = defaultModel.SpreadDetection,
        };
    }

    public string PresetKey { get; set; } = string.Empty;
    public string PresetName { get; set; } = "?";
    public bool OriginalSize { get; set; } = false;
    public bool IsVertical { get; set; } = true;
    public bool IsLeftToRight { get; set; } = false;
    public bool IsVerticalContinuous { get; set; } = false;
    public bool IsHorizontalContinuous { get; set; } = false;
    public PageLayoutSettings VerticalPageLayout { get; set; } = new();
    public PageLayoutSettings HorizontalPageLayout { get; set; } = new();
    public int PageSpacing { get; set; } = 100;
    public int AutoScrollSpeed { get; set; } = 0;
    public ImageRotationEnum ImageRotation { get; set; } = ImageRotationEnum.None;
    public bool ImageFlip { get; set; } = false;
    public bool ImageInvert { get; set; } = false;
    public bool AntiAliasingFilter { get; set; } = false;

    public bool IsContinuous
    {
        get
        {
            return IsVertical ? IsVerticalContinuous : IsHorizontalContinuous;
        }
        set
        {
            if (IsVertical)
            {
                IsVerticalContinuous = value;
            }
            else
            {
                IsHorizontalContinuous = value;
            }
        }
    }

    public PageLayoutSettings PageLayout
    {
        get
        {
            return IsVertical ? VerticalPageLayout : HorizontalPageLayout;
        }
    }

    private ReaderSettingsModel() { }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj is not ReaderSettingsModel other)
        {
            return false;
        }

        return
            PresetKey == other.PresetKey &&
            PresetName == other.PresetName &&
            OriginalSize == other.OriginalSize &&
            IsVertical == other.IsVertical &&
            IsLeftToRight == other.IsLeftToRight &&
            IsVerticalContinuous == other.IsVerticalContinuous &&
            IsHorizontalContinuous == other.IsHorizontalContinuous &&
            VerticalPageLayout == other.VerticalPageLayout &&
            HorizontalPageLayout == other.HorizontalPageLayout &&
            PageSpacing == other.PageSpacing &&
            AutoScrollSpeed == other.AutoScrollSpeed &&
            ImageRotation == other.ImageRotation &&
            ImageFlip == other.ImageFlip &&
            ImageInvert == other.ImageInvert &&
            AntiAliasingFilter == other.AntiAliasingFilter;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(PresetKey);
        hash.Add(PresetName);
        hash.Add(OriginalSize);
        hash.Add(IsVertical);
        hash.Add(IsLeftToRight);
        hash.Add(IsVerticalContinuous);
        hash.Add(IsHorizontalContinuous);
        hash.Add(VerticalPageLayout);
        hash.Add(HorizontalPageLayout);
        hash.Add(PageSpacing);
        hash.Add(AutoScrollSpeed);
        hash.Add(ImageRotation);
        hash.Add(ImageFlip);
        hash.Add(ImageInvert);
        hash.Add(AntiAliasingFilter);
        return hash.ToHashCode();
    }

    public ReaderSettingsModel Clone()
    {
        var cloned = (ReaderSettingsModel)MemberwiseClone();
        cloned.VerticalPageLayout = VerticalPageLayout.Clone();
        cloned.HorizontalPageLayout = HorizontalPageLayout.Clone();
        return cloned;
    }

    public static bool operator ==(ReaderSettingsModel? left, ReaderSettingsModel? right)
    {
        return EqualityComparer<ReaderSettingsModel>.Default.Equals(left, right);
    }

    public static bool operator !=(ReaderSettingsModel? left, ReaderSettingsModel? right)
    {
        return !(left == right);
    }

    public JsonModel ToJsonModel()
    {
        return new()
        {
            PresetName = PresetName,
            OriginalSize = OriginalSize,
            VerticalReading = IsVertical,
            LeftToRight = IsLeftToRight,
            VerticalContinuous = IsVerticalContinuous,
            HorizontalContinuous = IsHorizontalContinuous,
            VerticalPageLayout = VerticalPageLayout.ToJsonModel(),
            HorizontalPageLayout = HorizontalPageLayout.ToJsonModel(),
            PageSpacing = PageSpacing,
            AutoScrollSpeed = AutoScrollSpeed,
            ImageRotation = ImageRotation switch
            {
                ImageRotationEnum.None => "None",
                ImageRotationEnum.Rotate90 => "Rotate90",
                ImageRotationEnum.Rotate180 => "Rotate180",
                ImageRotationEnum.Rotate270 => "Rotate270",
                _ => "None",
            },
            ImageFlip = ImageFlip,
            ImageInvert = ImageInvert,
            AntiAliasingFilter = AntiAliasingFilter,
        };
    }

    public void SaveToComic(ComicModel comic)
    {
        JsonModel jsonModel = ToJsonModel();
        string customSettingsJson = JsonSerializer.Serialize(jsonModel);
        comic.SetExt(ComicExt.READER_SETTING_PRESET_KEY, PresetKey);
        comic.SetExt(ComicExt.CUSTOM_READER_SETTINGS, customSettingsJson);
        CoroutineUtils.Run(comic.FlushExt);
    }

    public class JsonModel
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

        [JsonPropertyName("VerticalPageLayout")]
        public PageLayoutSettings.JsonModel? VerticalPageLayout { get; set; }

        [JsonPropertyName("HorizontalPageLayout")]
        public PageLayoutSettings.JsonModel? HorizontalPageLayout { get; set; }

        [JsonPropertyName("PageGap")]
        public int? PageSpacing { get; set; }

        [JsonPropertyName("AutoScrollSpeed")]
        public int? AutoScrollSpeed { get; set; }

        [JsonPropertyName("ImageRotation")]
        public string? ImageRotation { get; set; }

        [JsonPropertyName("ImageFlip")]
        public bool? ImageFlip { get; set; }

        [JsonPropertyName("ImageInvert")]
        public bool? ImageInvert { get; set; }

        [JsonPropertyName("AntiAliasingFilter")]
        public bool? AntiAliasingFilter { get; set; }

        //
        // Legacy fields
        //

        [JsonPropertyName("VerticalPageArrangement")]
        public int? LegacyVerticalPageArrangement { get; set; }

        [JsonPropertyName("HorizontalPageArrangement")]
        public int? LegacyHorizontalPageArrangement { get; set; }
    }
}
