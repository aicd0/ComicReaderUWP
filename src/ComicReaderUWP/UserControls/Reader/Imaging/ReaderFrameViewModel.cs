// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.UserControls.Reader.FrameLayout;

using Microsoft.UI.Xaml;

namespace ComicReaderUWP.UserControls.Reader.Imaging;

internal class ReaderFrameViewModel : IReaderListViewItemViewModel
{
    public const int NO_PAGE = -1;

    public readonly MutableLiveData<bool> RedrawImageLiveDate = new();
    public readonly MutableLiveData<double> ScaleLiveData = new();

    public required Thickness FrameMargin { get; init; }
    public required ReaderImageSource? LeftImageSource { get; init; }
    public required double LeftImageWidth { get; init; }
    public required double LeftImageHeight { get; init; }
    public required ReaderImageSource? RightImageSource { get; init; }
    public required double RightImageWidth { get; init; }
    public required double RightImageHeight { get; init; }
    public required Func<int, Task<IReadOnlyList<BaseMenuFlyoutItemModel>>> RequestImageContextMenu { get; init; }

    public double FrameWidth => LeftImageWidth + RightImageWidth;
    public double FrameHeight => Math.Max(LeftImageHeight, RightImageHeight);
    public int PageL { get; init; } = NO_PAGE;
    public int PageR { get; init; } = NO_PAGE;
    public double Page => PageL != NO_PAGE && PageR != NO_PAGE ? (PageL + PageR) * 0.5 : (PageL == NO_PAGE ? PageR : PageL);
    public bool IsDualPage => PageL != NO_PAGE && PageR != NO_PAGE;
    public bool IsEmpty => PageL == NO_PAGE && PageR == NO_PAGE;
    public int MaxPage => Math.Max(PageL, PageR);
    public int MinPage => PageL == NO_PAGE ? PageR : (PageR == NO_PAGE ? PageL : Math.Min(PageL, PageR));
    public int PageCount => (PageL != NO_PAGE ? 1 : 0) + (PageR != NO_PAGE ? 1 : 0);

    double IReaderListViewItemViewModel.Width => FrameWidth;

    double IReaderListViewItemViewModel.Height => FrameHeight;

    Thickness IReaderListViewItemViewModel.Margin => FrameMargin;

    public void RedrawImage()
    {
        RedrawImageLiveDate.Emit(true);
    }

    public void SetScale(double scale)
    {
        if (ScaleLiveData.HasValue && ScaleLiveData.Value == scale)
        {
            return;
        }

        ScaleLiveData.Emit(scale);
    }
};
