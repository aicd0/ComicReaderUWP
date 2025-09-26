// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;

namespace ComicReader.Views.Pages.Navigation;

internal class ReaderSettingDataModel
{
    public bool OriginalSize { get; set; } = false;
    public bool UseDefault { get; set; } = true;
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
            OriginalSize = OriginalSize,
            UseDefault = UseDefault,
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

    public void To(AppSettingsModel.ReaderSettingModel model)
    {
        model.OriginalSize = OriginalSize;
        model.VerticalReading = IsVertical;
        model.LeftToRight = IsLeftToRight;
        model.VerticalContinuous = IsVerticalContinuous;
        model.HorizontalContinuous = IsHorizontalContinuous;
        model.VerticalPageArrangement = VerticalPageArrangement;
        model.HorizontalPageArrangement = HorizontalPageArrangement;
        model.PageGap = PageGap;
        model.AutoScrollSpeed = AutoScrollSpeed;
    }

    public void To(ComicModel comic)
    {
        comic.SetExt(ComicExt.ORIGINAL_SIZE, OriginalSize ? "1" : "0");
        comic.SetExt(ComicExt.USE_DEFAULT_READER_SETTINGS, UseDefault ? "1" : "0");
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

    public static ReaderSettingDataModel From(AppSettingsModel.ReaderSettingModel model, ComicModel comic)
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

        bool useDefault = comic.GetExt(ComicExt.USE_DEFAULT_READER_SETTINGS)?.Equals("1") ?? true;
        bool originalSize;
        bool verticalReading;
        bool leftToRight;
        bool verticalContinuous;
        bool horizontalContinuous;
        PageArrangementEnum verticalPageArrangement;
        PageArrangementEnum horizontalPageArrangement;
        int pageGap;
        int autoScrollSpeed;

        if (useDefault)
        {
            originalSize = model.OriginalSize;
            verticalReading = model.VerticalReading;
            leftToRight = model.LeftToRight;
            verticalContinuous = model.VerticalContinuous;
            horizontalContinuous = model.HorizontalContinuous;
            verticalPageArrangement = model.VerticalPageArrangement;
            horizontalPageArrangement = model.HorizontalPageArrangement;
            pageGap = model.PageGap;
            autoScrollSpeed = model.AutoScrollSpeed;
        }
        else
        {
            originalSize = comic.GetExt(ComicExt.ORIGINAL_SIZE)?.Equals("1") ?? model.OriginalSize;
            verticalReading = comic.GetExt(ComicExt.VERTICAL_READING)?.Equals("1") ?? model.VerticalReading;
            leftToRight = comic.GetExt(ComicExt.LEFT_TO_RIGHT)?.Equals("1") ?? model.LeftToRight;
            verticalContinuous = comic.GetExt(ComicExt.VERTICAL_CONTINUOUS)?.Equals("1") ?? model.VerticalContinuous;
            horizontalContinuous = comic.GetExt(ComicExt.HORIZONTAL_CONTINUOUS)?.Equals("1") ?? model.HorizontalContinuous;
            verticalPageArrangement = ParsePageArrangement(comic.GetExt(ComicExt.VERTICAL_PAGE_ARRANGEMENT)) ?? model.VerticalPageArrangement;
            horizontalPageArrangement = ParsePageArrangement(comic.GetExt(ComicExt.HORIZONTAL_PAGE_ARRANGEMENT)) ?? model.HorizontalPageArrangement;

            pageGap = model.PageGap;
            {
                string? pageGapString = comic.GetExt(ComicExt.PAGE_GAP);
                if (!string.IsNullOrEmpty(pageGapString) && int.TryParse(pageGapString, out int parsedPageGap))
                {
                    pageGap = parsedPageGap;
                }
            }

            autoScrollSpeed = model.AutoScrollSpeed;
            {
                string? autoScrollSpeedString = comic.GetExt(ComicExt.AUTO_SCROLL_SPEED);
                if (!string.IsNullOrEmpty(autoScrollSpeedString) && int.TryParse(autoScrollSpeedString, out int parsedAutoScrollSpeed))
                {
                    autoScrollSpeed = parsedAutoScrollSpeed;
                }
            }
        }

        return new ReaderSettingDataModel
        {
            OriginalSize = originalSize,
            UseDefault = useDefault,
            IsVertical = verticalReading,
            IsLeftToRight = leftToRight,
            IsVerticalContinuous = verticalContinuous,
            IsHorizontalContinuous = horizontalContinuous,
            VerticalPageArrangement = verticalPageArrangement,
            HorizontalPageArrangement = horizontalPageArrangement,
            PageGap = pageGap,
            AutoScrollSpeed = autoScrollSpeed,
        };
    }
}
