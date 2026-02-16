// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Utils;

namespace ComicReaderUWP.Views.Pages.Main;

internal class ReaderSettingsModel
{
    public const string PRESET_KEY_DEFAULT = "###default###";
    public const string PRESET_KEY_CUSTOM = "###custom###";
    private const string TAG = nameof(ReaderSettingsModel);

    public string PresetKey { get; set; } = string.Empty;
    public string PresetName { get; set; } = "?";
    public bool OriginalSize { get; set; } = false;
    public bool IsVertical { get; set; } = true;
    public bool IsLeftToRight { get; set; } = false;
    public bool IsVerticalContinuous { get; set; } = false;
    public bool IsHorizontalContinuous { get; set; } = false;
    public PageArrangementEnum VerticalPageArrangement { get; set; } = PageArrangementEnum.Single;
    public PageArrangementEnum HorizontalPageArrangement { get; set; } = PageArrangementEnum.DualCover;
    public int PageGap { get; set; } = 100;
    public int AutoScrollSpeed { get; set; } = 0;
    public ImageRotationEnum ImageRotation { get; set; } = ImageRotationEnum.None;
    public bool ImageFlip { get; set; } = false;
    public bool ImageInvert { get; set; } = false;

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

    public PageArrangementEnum PageArrangement
    {
        get
        {
            return IsVertical ? VerticalPageArrangement : HorizontalPageArrangement;
        }
    }

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

        return PresetKey == other.PresetKey &&
            PresetName == other.PresetName &&
            OriginalSize == other.OriginalSize &&
            IsVertical == other.IsVertical &&
            IsLeftToRight == other.IsLeftToRight &&
            IsVerticalContinuous == other.IsVerticalContinuous &&
            IsHorizontalContinuous == other.IsHorizontalContinuous &&
            VerticalPageArrangement == other.VerticalPageArrangement &&
            HorizontalPageArrangement == other.HorizontalPageArrangement &&
            PageGap == other.PageGap &&
            AutoScrollSpeed == other.AutoScrollSpeed &&
            IsContinuous == other.IsContinuous &&
            PageArrangement == other.PageArrangement &&
            ImageRotation == other.ImageRotation &&
            ImageFlip == other.ImageFlip &&
            ImageInvert == other.ImageInvert;
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
        hash.Add(VerticalPageArrangement);
        hash.Add(HorizontalPageArrangement);
        hash.Add(PageGap);
        hash.Add(AutoScrollSpeed);
        hash.Add(IsContinuous);
        hash.Add(PageArrangement);
        hash.Add(ImageRotation);
        hash.Add(ImageFlip);
        hash.Add(ImageInvert);
        return hash.ToHashCode();
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
            VerticalPageArrangement = (int)VerticalPageArrangement,
            HorizontalPageArrangement = (int)HorizontalPageArrangement,
            PageGap = PageGap,
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
        };
    }

    public void SaveToComic(ComicModel comic)
    {
        JsonModel jsonModel = ToJsonModel();
        string customSettingsJson = JsonSerializer.Serialize(jsonModel);
        comic.SetExt(ComicExt.READER_SETTING_PRESET_KEY, PresetKey);
        comic.SetExt(ComicExt.CUSTOM_READER_SETTINGS, customSettingsJson);
        CoroutineUtils.Start(comic.FlushExt);
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
            VerticalPageArrangement = ParsePageArrangementEnum(model.VerticalPageArrangement) ?? defaultModel.VerticalPageArrangement,
            HorizontalPageArrangement = ParsePageArrangementEnum(model.HorizontalPageArrangement) ?? defaultModel.HorizontalPageArrangement,
            PageGap = model.PageGap ?? defaultModel.PageGap,
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
        };
    }

    public static ReaderSettingsModel LoadFromComic(ComicModel comic)
    {
        string presetKey = comic.GetExt(ComicExt.READER_SETTING_PRESET_KEY) ?? AppSettingsModel.Instance.DefaultReaderSettingPresetKey;
        if (presetKey == PRESET_KEY_CUSTOM)
        {
            JsonModel? jsonModel = null;
            string? customSettingsJson = comic.GetExt(ComicExt.CUSTOM_READER_SETTINGS);
            if (!string.IsNullOrEmpty(customSettingsJson))
            {
                try
                {
                    jsonModel = JsonSerializer.Deserialize<JsonModel>(customSettingsJson);
                }
                catch (JsonException e)
                {
                    Logger.E(TAG, e);
                }
            }

            ReaderSettingsModel model = FromJsonModel(presetKey, jsonModel);
            return model;
        }

        Dictionary<string, ReaderSettingsModel> presets = AppSettingsModel.Instance.ReaderSettingPresets;
        if (!presets.TryGetValue(presetKey, out ReaderSettingsModel? presetModel))
        {
            if (!presets.TryGetValue(AppSettingsModel.Instance.DefaultReaderSettingPresetKey, out presetModel))
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
            return new()
            {
                PresetKey = PRESET_KEY_DEFAULT,
                PresetName = StringResourceProvider.Instance.Default,
            };
        }

        return presetModel;
    }

    private static PageArrangementEnum? ParsePageArrangementEnum(int? value)
    {
        if (value.HasValue && Enum.IsDefined(typeof(PageArrangementEnum), value))
        {
            return (PageArrangementEnum)value;
        }

        return null;
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

        [JsonPropertyName("VerticalPageArrangement")]
        public int? VerticalPageArrangement { get; set; }

        [JsonPropertyName("HorizontalPageArrangement")]
        public int? HorizontalPageArrangement { get; set; }

        [JsonPropertyName("PageGap")]
        public int? PageGap { get; set; }

        [JsonPropertyName("AutoScrollSpeed")]
        public int? AutoScrollSpeed { get; set; }

        [JsonPropertyName("ImageRotation")]
        public string? ImageRotation { get; set; }

        [JsonPropertyName("ImageFlip")]
        public bool? ImageFlip { get; set; }

        [JsonPropertyName("ImageInvert")]
        public bool? ImageInvert { get; set; }
    }
}
