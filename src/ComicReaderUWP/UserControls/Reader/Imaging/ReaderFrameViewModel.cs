// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.UserControls.Reader.FrameLayout;

using Microsoft.UI.Xaml;

namespace ComicReaderUWP.UserControls.Reader.Imaging;

internal class ReaderFrameViewModel : IReaderListViewItemViewModel
{
    public const int NO_PAGE = -1;

    public readonly MutableLiveData<bool> RebindLiveData = new();
    public readonly MutableLiveData<bool> RedrawImageLiveDate = new();
    public readonly MutableLiveData<double> ScaleLiveData = new();

    public Thickness FrameMargin { get; set; } = new(0.0, 0.0, 0.0, 0.0);
    public ReaderImageSource? LeftImageSource { get; set; }
    public double LeftImageWidth { get; set; } = 0.0;
    public double LeftImageHeight { get; set; } = 0.0;
    public ReaderImageSource? RightImageSource { get; set; }
    public double RightImageWidth { get; set; } = 0.0;
    public double RightImageHeight { get; set; } = 0.0;
    public double FrameWidth => LeftImageWidth + RightImageWidth;
    public double FrameHeight => Math.Max(LeftImageHeight, RightImageHeight);

    public int PageL { get; set; } = NO_PAGE;
    public int PageR { get; set; } = NO_PAGE;
    public double Page => PageL != NO_PAGE && PageR != NO_PAGE ? (PageL + PageR) * 0.5 : (PageL == NO_PAGE ? PageR : PageL);
    public bool IsDualPage => PageL != NO_PAGE && PageR != NO_PAGE;
    public bool IsEmpty => PageL == NO_PAGE && PageR == NO_PAGE;
    public int MaxPage => Math.Max(PageL, PageR);
    public int MinPage => PageL == NO_PAGE ? PageR : (PageR == NO_PAGE ? PageL : Math.Min(PageL, PageR));
    public int PageCount => (PageL != NO_PAGE ? 1 : 0) + (PageR != NO_PAGE ? 1 : 0);

    double IReaderListViewItemViewModel.Width => FrameWidth + FrameMargin.Left + FrameMargin.Right;

    double IReaderListViewItemViewModel.Height => FrameHeight + FrameMargin.Top + FrameMargin.Bottom;

    public void RebindEntireViewModel()
    {
        RebindLiveData.Emit(true);
    }

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
