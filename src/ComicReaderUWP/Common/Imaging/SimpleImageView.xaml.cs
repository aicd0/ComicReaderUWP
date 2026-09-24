// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Storage;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Helpers.Imaging;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace ComicReaderUWP.Common.Imaging;

internal partial class SimpleImageView : UserControl
{
    public static readonly DependencyProperty UriProperty = DependencyProperty.Register(
        nameof(Uri),
        typeof(string),
        typeof(SimpleImageView),
        new PropertyMetadata(null, OnImagePropertyChanged));

    public static readonly DependencyProperty FrameWidthProperty = DependencyProperty.Register(
        nameof(FrameWidth),
        typeof(double),
        typeof(SimpleImageView),
        new PropertyMetadata(double.PositiveInfinity, OnImagePropertyChanged));

    public static readonly DependencyProperty FrameHeightProperty = DependencyProperty.Register(
        nameof(FrameHeight),
        typeof(double),
        typeof(SimpleImageView),
        new PropertyMetadata(double.PositiveInfinity, OnImagePropertyChanged));

    public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
        nameof(Stretch),
        typeof(Stretch),
        typeof(SimpleImageView),
        new PropertyMetadata(Stretch.Uniform, OnStretchChanged));

    private static void OnImagePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((SimpleImageView)sender).UpdateImage();
    }

    private static void OnStretchChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var view = (SimpleImageView)sender;
        view.ImageHolder.Stretch = (Stretch)args.NewValue;
        view.UpdateImage();
    }

    private static async Task<IImageSource?> ResolveImageSource(string? uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return null;
        }

        if (ResourceUri.TryParse(uri, out ResourceUri? resourceUri))
        {
            return await resourceUri.ResolveImage();
        }

        return File.Exists(uri) ? new LocalFileImageSource(uri) : null;
    }

    private readonly CancellationSession _cancellationSession = new();
    private bool _isLoaded = false;
    private int _currentImageHash = 0;

    public SimpleImageView()
    {
        InitializeComponent();
        Loaded += SimpleImageView_LoadedOrUnloaded;
        Unloaded += SimpleImageView_LoadedOrUnloaded;
    }

    public string? Uri
    {
        get => (string?)GetValue(UriProperty);
        set => SetValue(UriProperty, value);
    }

    public double FrameWidth
    {
        get => (double)GetValue(FrameWidthProperty);
        set => SetValue(FrameWidthProperty, value);
    }

    public double FrameHeight
    {
        get => (double)GetValue(FrameHeightProperty);
        set => SetValue(FrameHeightProperty, value);
    }

    public Stretch Stretch
    {
        get => (Stretch)GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    private void SimpleImageView_LoadedOrUnloaded(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == IsLoaded)
        {
            return;
        }

        _isLoaded = IsLoaded;
        if (_isLoaded)
        {
            UpdateImage();
        }
        else
        {
            UnloadImage();
        }
    }

    private void UpdateImage()
    {
        if (!_isLoaded)
        {
            return;
        }

        string? uri = Uri;
        double frameWidth = FrameWidth;
        double frameHeight = FrameHeight;
        Stretch stretch = Stretch;
        int newHash = HashCode.Combine(uri, frameWidth, frameHeight, stretch);
        if (newHash == _currentImageHash)
        {
            return;
        }

        UnloadImage();
        _currentImageHash = newHash;
        CancellationSession.IToken token = _cancellationSession.Token;
        IImageResultHandler handler = new WeakImageResultHandler(this);

        CoroutineUtils.Run(async () =>
        {
            IImageSource source = await ResolveImageSource(uri) ?? EmptyImageSource.Instance;
            await ImageLoader.LoadImage(source, new()
            {
                Token = token,
                FrameWidth = frameWidth,
                FrameHeight = frameHeight,
                Stretch = stretch,
                Handler = handler,
            });
        });
    }

    private void UnloadImage()
    {
        _currentImageHash = 0;
        _cancellationSession.Next();
        ImageHolder.Source = null;
    }

    private class WeakImageResultHandler(SimpleImageView view) : IImageResultHandler
    {
        private readonly WeakReference<SimpleImageView> _imageViewRef = new(view);

        public void OnSuccess(DecodedImageModel result)
        {
            if (!_imageViewRef.TryGetTarget(out SimpleImageView? view) || !view.IsLoaded)
            {
                return;
            }

            view.ImageHolder.Source = result.Source;
        }

        public void OnFailure()
        {
        }
    }
}
