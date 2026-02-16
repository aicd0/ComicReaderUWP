// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.ComponentModel;

using ComicReaderUWP.SDK.Common.Threading;

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

    public ReaderImageModel? LeftImageSource { get; set; }
    public double LeftImageWidth { get; set; } = 0.0;
    public double LeftImageHeight { get; set; } = 0.0;

    public ReaderImageModel? RightImageSource { get; set; }
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

    private readonly ReaderImageSourceHolder _imageSourceHolder;

    public ReaderFrameViewModel(ITaskDispatcher loadImageDispatcher)
    {
        _imageSourceHolder = new(loadImageDispatcher);
        _imageSourceHolder.SourceChanged += source =>
        {
            ImageMerged = source;
        };
    }

    public void Dispose()
    {
        _imageSourceHolder.Dispose();
    }

    public void RebindEntireViewModel()
    {
        PropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(ReaderFrameViewModel)));
    }

    public void SetLeftImageVisibility(bool visible)
    {
        _imageSourceHolder.PlaceholderMode = IsDualPage;
        _imageSourceHolder.SetImage(0, visible ? LeftImageSource : null, LeftImageWidth, LeftImageHeight);
    }

    public void SetRightImageVisibility(bool visible)
    {
        _imageSourceHolder.PlaceholderMode = IsDualPage;
        _imageSourceHolder.SetImage(1, visible ? RightImageSource : null, RightImageWidth, RightImageHeight);
    }

    public void SetScale(double scale)
    {
        _imageSourceHolder.Scale = scale;
    }
};
