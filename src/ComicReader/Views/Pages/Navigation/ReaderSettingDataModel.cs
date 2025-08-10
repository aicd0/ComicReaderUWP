// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Data.Models;

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
        };
        return clone;
    }
}
