// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.ComponentModel;

using Microsoft.UI.Xaml;

namespace ComicReaderUWP.UserControls.Reader.Imaging;

internal partial class ReaderFrameViewModel : INotifyPropertyChanged
{
    public const int NO_PAGE = -1;

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _leftImageVisible = false;
    public bool LeftImageVisible
    {
        get => _leftImageVisible;
        set
        {
            _leftImageVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LeftImageVisible)));
        }
    }

    private bool _rightImageVisible = false;
    public bool RightImageVisible
    {
        get => _rightImageVisible;
        set
        {
            _rightImageVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RightImageVisible)));
        }
    }

    private double _scale = 1.0;
    public double Scale
    {
        get => _scale;
        set
        {
            _scale = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Scale)));
        }
    }

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

    public void RebindEntireViewModel()
    {
        PropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(ReaderFrameViewModel)));
    }

    public void SetLeftImageVisibility(bool visible)
    {
        LeftImageVisible = visible;
    }

    public void SetRightImageVisibility(bool visible)
    {
        RightImageVisible = visible;
    }

    public void SetScale(double scale)
    {
        Scale = scale;
    }
};
