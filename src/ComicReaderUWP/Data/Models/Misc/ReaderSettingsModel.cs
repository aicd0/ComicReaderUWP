// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;

namespace ComicReaderUWP.Data.Models.Misc;

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

    public static ReaderSettingsModel FromJsonModel(string key, JsonModel? model, ReaderSettingsModel? fallbackModel = null)
    {
        fallbackModel ??= new();
        fallbackModel.PresetKey = key;

        if (model is null)
        {
            return fallbackModel;
        }

        return new()
        {
            PresetKey = key,
            PresetName = model.PresetName ?? fallbackModel.PresetName,
            OriginalSize = model.OriginalSize ?? fallbackModel.OriginalSize,
            IsVertical = model.VerticalReading ?? fallbackModel.IsVertical,
            IsLeftToRight = model.LeftToRight ?? fallbackModel.IsLeftToRight,
            IsVerticalContinuous = model.VerticalContinuous ?? fallbackModel.IsVerticalContinuous,
            IsHorizontalContinuous = model.HorizontalContinuous ?? fallbackModel.IsHorizontalContinuous,
            VerticalPageLayout = PageLayoutSettings.FromJsonModel(model.VerticalPageLayout, fallbackModel.VerticalPageLayout),
            HorizontalPageLayout = PageLayoutSettings.FromJsonModel(model.HorizontalPageLayout, fallbackModel.HorizontalPageLayout),
            PageSpacing = model.PageSpacing ?? fallbackModel.PageSpacing,
            AutoScrollSpeed = model.AutoScrollSpeed ?? fallbackModel.AutoScrollSpeed,
            ImageRotation = model.ImageRotation switch
            {
                "None" => ImageRotationEnum.None,
                "Rotate90" => ImageRotationEnum.Rotate90,
                "Rotate180" => ImageRotationEnum.Rotate180,
                "Rotate270" => ImageRotationEnum.Rotate270,
                _ => fallbackModel.ImageRotation,
            },
            ImageFlip = model.ImageFlip ?? fallbackModel.ImageFlip,
            AntiAliasingFilterPercentage = model.AntiAliasingFilterPercentage ?? fallbackModel.AntiAliasingFilterPercentage,
            BrightnessPercentage = model.BrightnessPercentage ?? fallbackModel.BrightnessPercentage,
            ContrastPercentage = model.ContrastPercentage ?? fallbackModel.ContrastPercentage,
            SaturationPercentage = model.SaturationPercentage ?? fallbackModel.SaturationPercentage,
            ImageInvert = model.ImageInvert ?? fallbackModel.ImageInvert,
        };
    }

    public static ReaderSettingsModel LoadFromComic(ComicModel comic)
    {
        string defaultPresetKey = AppSettingsModel.DefaultReaderSettingPresetKey;
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
            Dictionary<string, ReaderSettingsModel> presets = AppSettingsModel.ReaderSettingPresets;
            if (!presets.TryGetValue(presetKey, out presetModel))
            {
                if (!presets.TryGetValue(defaultPresetKey, out presetModel))
                {
                    foreach (ReaderSettingsModel preset in presets.Values)
                    {
                        presetModel = preset;
                        break;
                    }
                }
            }

            if (presetModel is null)
            {
                // Automatically import missing key, especially for default preset
                presetModel = FromDefault();
                presets[presetKey] = presetModel;
                AppSettingsModel.ReaderSettingPresets = presets;
            }
            else
            {
                presetKey = presetModel.PresetKey;
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
            ReaderSettingsModel model = FromJsonModel(presetKey, jsonModel, presetModel);
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

        Dictionary<string, ReaderSettingsModel> presets = AppSettingsModel.ReaderSettingPresets;
        if (!presets.TryGetValue(presetKey, out ReaderSettingsModel? presetModel))
        {
            return null;
        }

        return presetModel;
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
    public int AntiAliasingFilterPercentage { get; set; } = 0;
    public int BrightnessPercentage { get; set; } = 50;
    public int ContrastPercentage { get; set; } = 50;
    public int SaturationPercentage { get; set; } = 100;
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

    public PageLayoutSettings PageLayout
    {
        get
        {
            return IsVertical ? VerticalPageLayout : HorizontalPageLayout;
        }
    }

    private ReaderSettingsModel() { }

    public ReaderSettingsModel Clone()
    {
        var cloned = (ReaderSettingsModel)MemberwiseClone();
        cloned.VerticalPageLayout = VerticalPageLayout.Clone();
        cloned.HorizontalPageLayout = HorizontalPageLayout.Clone();
        return cloned;
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
            AntiAliasingFilterPercentage = AntiAliasingFilterPercentage,
            BrightnessPercentage = BrightnessPercentage,
            ContrastPercentage = ContrastPercentage,
            SaturationPercentage = SaturationPercentage,
            ImageInvert = ImageInvert,
        };
    }

    public void SaveToComic(ComicModel comic)
    {
        JsonModel? jsonModel = ToJsonModel();

        ReaderSettingsModel? diffModel = LoadFromPreset(PresetKey);
        if (diffModel is not null)
        {
            var diffJsonModel = diffModel.ToJsonModel();
            jsonModel = NullOutMatchingFields(jsonModel, diffJsonModel);
        }

        string customSettingsJson = JsonSerializer.Serialize(jsonModel);
        comic.SetExt(ComicExt.READER_SETTING_PRESET_KEY, PresetKey);
        comic.SetExt(ComicExt.CUSTOM_READER_SETTINGS, customSettingsJson);
        CoroutineUtils.Run(comic.FlushExt);
    }

    public static T? NullOutMatchingFields<T>(T? first, T? second) where T : class
    {
        bool Diff(JsonNode? first, JsonNode? second)
        {
            if (first is null || second is null)
            {
                return first == second;
            }

            if (first is JsonValue && second is JsonValue)
            {
                return JsonNode.DeepEquals(first, second);
            }

            if (first is JsonObject firstObj && second is JsonObject secondObj)
            {
                bool allEqual = true;

                foreach (KeyValuePair<string, JsonNode?> kvp in firstObj.ToList())
                {
                    if (!secondObj.TryGetPropertyValue(kvp.Key, out JsonNode? secondValue))
                    {
                        allEqual = false;
                        continue;
                    }

                    JsonNode? firstValue = kvp.Value;

                    bool equal = Diff(firstValue, secondValue);

                    if (equal)
                    {
                        firstObj[kvp.Key] = null;
                    }
                    else
                    {
                        allEqual = false;
                    }
                }

                return allEqual;
            }

            if (first is JsonArray firstArr && second is JsonArray secondArr)
            {
                if (firstArr.Count != secondArr.Count)
                {
                    return false;
                }

                for (int i = 0; i < firstArr.Count; i++)
                {
                    bool equal = Diff(firstArr[i], secondArr[i]);

                    if (!equal)
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        if (first is null || second is null)
        {
            return first;
        }

        JsonNode firstNode = JsonSerializer.SerializeToNode(first)!;
        JsonNode secondNode = JsonSerializer.SerializeToNode(second)!;

        if (Diff(firstNode, secondNode))
        {
            return null;
        }

        return firstNode.Deserialize<T>()!;
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

        [JsonPropertyName("AntiAliasingFilterPercentage")]
        public int? AntiAliasingFilterPercentage { get; set; }

        [JsonPropertyName("BrightnessPercentage")]
        public int? BrightnessPercentage { get; set; }

        [JsonPropertyName("ContrastPercentage")]
        public int? ContrastPercentage { get; set; }

        [JsonPropertyName("SaturationPercentage")]
        public int? SaturationPercentage { get; set; }

        [JsonPropertyName("ImageInvert")]
        public bool? ImageInvert { get; set; }
    }
}
