// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.ComponentModel;

using ComicReaderUWP.Common.Imaging;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace ComicReaderUWP.UserControls.Reader;

internal partial class ReaderFrameViewModel : INotifyPropertyChanged, IDisposable
{
    public const int NO_PAGE = -1;

    public event PropertyChangedEventHandler? PropertyChanged;

    private ImageSource? _imageMerged;
    public ImageSource? ImageMerged
    {
        get => _imageMerged;
        set
        {
            _imageMerged = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageMerged)));
        }
    }

    public Thickness FrameMargin { get; set; } = new(0.0, 0.0, 0.0, 0.0);

    public IImageSource? LeftImageSource { get; set; }
    public ImageHolder LeftImageHolder { get; }
    public double LeftImageWidth { get; set; } = 0.0;
    public double LeftImageHeight { get; set; } = 0.0;

    public IImageSource? RightImageSource { get; set; }
    public ImageHolder RightImageHolder { get; }
    public double RightImageWidth { get; set; } = 0.0;
    public double RightImageHeight { get; set; } = 0.0;

    public double FrameWidth => LeftImageWidth + RightImageWidth;
    public double FrameHeight => Math.Max(LeftImageHeight, RightImageHeight);

    public int PageL { get; set; } = NO_PAGE;
    public int PageR { get; set; } = NO_PAGE;
    public double Page => PageL != NO_PAGE && PageR != NO_PAGE ? (PageL + PageR) * 0.5 : PageL == NO_PAGE ? PageR : PageL;
    public bool IsDualPage => PageL != NO_PAGE && PageR != NO_PAGE;

    private readonly ReaderImageSourceHolder _imageSourceHolder = new();

    public ReaderFrameViewModel(ReaderImagePool pool)
    {
        _imageSourceHolder.SourceChanged += source =>
        {
            ImageMerged = source;
        };
        LeftImageHolder = new(pool, source =>
        {
            PrepareImageSourceHolder();
            _imageSourceHolder.SetLeftImage(source);
        });
        RightImageHolder = new(pool, source =>
        {
            PrepareImageSourceHolder();
            _imageSourceHolder.SetRightImage(source);
        });
    }

    public void Dispose()
    {
        LeftImageHolder.Dispose();
        RightImageHolder.Dispose();
        _imageSourceHolder.Dispose();
    }

    public void RebindEntireViewModel()
    {
        PropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(ReaderFrameViewModel)));
    }

    public void SetScale(double scale)
    {
        _imageSourceHolder.Scale = scale;
        _imageSourceHolder.Invalidate();
    }

    private void PrepareImageSourceHolder()
    {
        _imageSourceHolder.PlaceholderMode = IsDualPage;
        _imageSourceHolder.LeftImageWidth = LeftImageWidth;
        _imageSourceHolder.LeftImageHeight = LeftImageHeight;
        _imageSourceHolder.RightImageWidth = RightImageWidth;
        _imageSourceHolder.RightImageHeight = RightImageHeight;
    }
};
