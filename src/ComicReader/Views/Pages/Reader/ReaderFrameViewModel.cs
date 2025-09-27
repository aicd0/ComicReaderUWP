// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.ComponentModel;

using ComicReader.Common.Imaging;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ComicReader.Views.Pages.Reader;

internal partial class ReaderFrameViewModel : INotifyPropertyChanged
{
    public const int NO_PAGE = -1;

    public event PropertyChangedEventHandler PropertyChanged;

    private Thickness _frameMargin = new(0.0, 0.0, 0.0, 0.0);
    public Thickness FrameMargin
    {
        get => _frameMargin;
        set
        {
            _frameMargin = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FrameMargin)));
        }
    }

    public IImageSource LeftImageSource { get; set; }
    public ImageHolder LeftImageHolder { get; }

    private BitmapImage _imageLeft;
    public BitmapImage ImageLeft
    {
        get => _imageLeft;
        set
        {
            _imageLeft = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageLeft)));
        }
    }

    private double _leftImageWidth = 0.0;
    public double LeftImageWidth
    {
        get => _leftImageWidth;
        set
        {
            _leftImageWidth = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LeftImageWidth)));
        }
    }

    private double _leftImageHeight = 0.0;
    public double LeftImageHeight
    {
        get => _leftImageHeight;
        set
        {
            _leftImageHeight = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LeftImageHeight)));
        }
    }

    public IImageSource RightImageSource { get; set; }
    public ImageHolder RightImageHolder { get; }

    private BitmapImage _imageRight;
    public BitmapImage ImageRight
    {
        get => _imageRight;
        set
        {
            _imageRight = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageRight)));
        }
    }

    private double _rightImageWidth = 0.0;
    public double RightImageWidth
    {
        get => _rightImageWidth;
        set
        {
            _rightImageWidth = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RightImageWidth)));
        }
    }

    private double _rightImageHeight = 0.0;
    public double RightImageHeight
    {
        get => _rightImageHeight;
        set
        {
            _rightImageHeight = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RightImageHeight)));
        }
    }

    public double FrameWidth => LeftImageWidth + RightImageWidth;
    public double FrameHeight => Math.Max(LeftImageHeight, RightImageHeight);

    public int PageL { get; set; } = NO_PAGE;
    public int PageR { get; set; } = NO_PAGE;
    public double Page => PageL != NO_PAGE && PageR != NO_PAGE ? (PageL + PageR) * 0.5 : PageL == NO_PAGE ? PageR : PageL;

    public ReaderFrameViewModel(ReaderImagePool pool)
    {
        LeftImageHolder = new(pool, delegate (BitmapImage image)
        {
            ImageLeft = image;
        });
        RightImageHolder = new(pool, delegate (BitmapImage image)
        {
            ImageRight = image;
        });
    }

    public void RebindEntireViewModel()
    {
        PropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(ReaderFrameViewModel)));
    }
};
