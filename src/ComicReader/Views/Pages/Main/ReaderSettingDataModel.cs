// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReader.Common;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;

namespace ComicReader.Views.Pages.Main;

internal class ReaderSettingDataModel
{
    public const string PRESET_KEY_DEFAULT = "###default###";
    public const string PRESET_KEY_CUSTOM = "###custom###";

    public string PresetKey { get; set; } = string.Empty;
    public string PresetName { get; set; } = string.Empty;
    public bool OriginalSize { get; set; } = false;
    public bool IsVertical { get; set; } = true;
    public bool IsLeftToRight { get; set; } = false;
    public bool IsVerticalContinuous { get; set; } = false;
    public bool IsHorizontalContinuous { get; set; } = false;
    public PageArrangementEnum VerticalPageArrangement { get; set; } = PageArrangementEnum.Single;
    public PageArrangementEnum HorizontalPageArrangement { get; set; } = PageArrangementEnum.DualCover;
    public int PageGap { get; set; } = 100;
    public int AutoScrollSpeed { get; set; } = 0;

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

    public ReaderSettingDataModel Clone()
    {
        var clone = new ReaderSettingDataModel
        {
            PresetKey = PresetKey,
            PresetName = PresetName,
            OriginalSize = OriginalSize,
            IsVertical = IsVertical,
            IsLeftToRight = IsLeftToRight,
            IsVerticalContinuous = IsVerticalContinuous,
            IsHorizontalContinuous = IsHorizontalContinuous,
            VerticalPageArrangement = VerticalPageArrangement,
            HorizontalPageArrangement = HorizontalPageArrangement,
            PageGap = PageGap,
            AutoScrollSpeed = AutoScrollSpeed,
        };
        return clone;
    }

    public AppSettingsModel.ReaderSettingModel ToSettingModel()
    {
        return new AppSettingsModel.ReaderSettingModel
        {
            PresetName = PresetName,
            OriginalSize = OriginalSize,
            VerticalReading = IsVertical,
            LeftToRight = IsLeftToRight,
            VerticalContinuous = IsVerticalContinuous,
            HorizontalContinuous = IsHorizontalContinuous,
            VerticalPageArrangement = VerticalPageArrangement,
            HorizontalPageArrangement = HorizontalPageArrangement,
            PageGap = PageGap,
            AutoScrollSpeed = AutoScrollSpeed,
        };
    }

    public void ToComic(ComicModel comic)
    {
        comic.SetExt(ComicExt.READER_SETTING_PRESET_KEY, PresetKey);
        comic.SetExt(ComicExt.ORIGINAL_SIZE, OriginalSize ? "1" : "0");
        comic.SetExt(ComicExt.VERTICAL_READING, IsVertical ? "1" : "0");
        comic.SetExt(ComicExt.LEFT_TO_RIGHT, IsLeftToRight ? "1" : "0");
        comic.SetExt(ComicExt.VERTICAL_CONTINUOUS, IsVerticalContinuous ? "1" : "0");
        comic.SetExt(ComicExt.HORIZONTAL_CONTINUOUS, IsHorizontalContinuous ? "1" : "0");
        comic.SetExt(ComicExt.VERTICAL_PAGE_ARRANGEMENT, VerticalPageArrangement.ToString());
        comic.SetExt(ComicExt.HORIZONTAL_PAGE_ARRANGEMENT, HorizontalPageArrangement.ToString());
        comic.SetExt(ComicExt.PAGE_GAP, PageGap.ToString());
        comic.SetExt(ComicExt.AUTO_SCROLL_SPEED, AutoScrollSpeed.ToString());
        comic.FlushExt();
    }

    public static ReaderSettingDataModel FromComic(ComicModel comic)
    {
        PageArrangementEnum? ParsePageArrangement(string? value)
        {
            if (value == null)
            {
                return null;
            }

            if (Enum.TryParse(value, out PageArrangementEnum arrangement))
            {
                return arrangement;
            }

            return null;
        }

        AppSettingsModel.ExternalModel settingModel = AppSettingsModel.Instance.GetModel();
        Dictionary<string, AppSettingsModel.ReaderSettingModel> presets = settingModel.ReaderSettingPresets;
        string presetKey = comic.GetExt(ComicExt.READER_SETTING_PRESET_KEY) ?? settingModel.DefaultReaderSettingPresetKey;
        var model = new ReaderSettingDataModel();

        if (presetKey == PRESET_KEY_CUSTOM)
        {
            model.OriginalSize = comic.GetExt(ComicExt.ORIGINAL_SIZE)?.Equals("1") ?? model.OriginalSize;
            model.IsVertical = comic.GetExt(ComicExt.VERTICAL_READING)?.Equals("1") ?? model.IsVertical;
            model.IsLeftToRight = comic.GetExt(ComicExt.LEFT_TO_RIGHT)?.Equals("1") ?? model.IsLeftToRight;
            model.IsVerticalContinuous = comic.GetExt(ComicExt.VERTICAL_CONTINUOUS)?.Equals("1") ?? model.IsVerticalContinuous;
            model.IsHorizontalContinuous = comic.GetExt(ComicExt.HORIZONTAL_CONTINUOUS)?.Equals("1") ?? model.IsHorizontalContinuous;
            model.VerticalPageArrangement = ParsePageArrangement(comic.GetExt(ComicExt.VERTICAL_PAGE_ARRANGEMENT)) ?? model.VerticalPageArrangement;
            model.HorizontalPageArrangement = ParsePageArrangement(comic.GetExt(ComicExt.HORIZONTAL_PAGE_ARRANGEMENT)) ?? model.HorizontalPageArrangement;

            {
                string? pageGapString = comic.GetExt(ComicExt.PAGE_GAP);
                if (!string.IsNullOrEmpty(pageGapString) && int.TryParse(pageGapString, out int parsedPageGap))
                {
                    model.PageGap = parsedPageGap;
                }
            }

            {
                string? autoScrollSpeedString = comic.GetExt(ComicExt.AUTO_SCROLL_SPEED);
                if (!string.IsNullOrEmpty(autoScrollSpeedString) && int.TryParse(autoScrollSpeedString, out int parsedAutoScrollSpeed))
                {
                    model.AutoScrollSpeed = parsedAutoScrollSpeed;
                }
            }
        }
        else
        {
            if (!presets.TryGetValue(presetKey, out AppSettingsModel.ReaderSettingModel? presetModel))
            {
                if (!presets.TryGetValue(settingModel.DefaultReaderSettingPresetKey, out presetModel))
                {
                    foreach (KeyValuePair<string, AppSettingsModel.ReaderSettingModel> kvp in presets)
                    {
                        presetKey = kvp.Key;
                        presetModel = kvp.Value;
                        break;
                    }
                }
            }

            if (presetModel is null)
            {
                presetKey = PRESET_KEY_DEFAULT;
                model.PresetName = StringResourceProvider.Instance.Default;
            }
            else
            {
                model.PresetName = presetModel.PresetName;
                model.OriginalSize = presetModel.OriginalSize;
                model.IsVertical = presetModel.VerticalReading;
                model.IsLeftToRight = presetModel.LeftToRight;
                model.IsVerticalContinuous = presetModel.VerticalContinuous;
                model.IsHorizontalContinuous = presetModel.HorizontalContinuous;
                model.VerticalPageArrangement = presetModel.VerticalPageArrangement;
                model.HorizontalPageArrangement = presetModel.HorizontalPageArrangement;
                model.PageGap = presetModel.PageGap;
                model.AutoScrollSpeed = presetModel.AutoScrollSpeed;
            }
        }

        model.PresetKey = presetKey;
        return model;
    }
}
