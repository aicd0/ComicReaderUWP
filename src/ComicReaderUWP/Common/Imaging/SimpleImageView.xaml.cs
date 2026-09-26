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

using Windows.Foundation;

namespace ComicReaderUWP.Common.Imaging;

internal partial class SimpleImageView : UserControl
{
    private const long RELOAD_INTERVAL_MS = 500;

    public static readonly DependencyProperty UriProperty = DependencyProperty.Register(
        nameof(Uri),
        typeof(string),
        typeof(SimpleImageView),
        new PropertyMetadata(null, OnUriChanged));

    public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
        nameof(Stretch),
        typeof(Stretch),
        typeof(SimpleImageView),
        new PropertyMetadata(Stretch.Uniform, OnStretchChanged));

    private static void OnUriChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((SimpleImageView)sender).LoadUri();
    }

    private static void OnStretchChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var view = (SimpleImageView)sender;
        view.ImageHolder.Stretch = (Stretch)args.NewValue;
        view.RequestReload();
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

    private bool _isLoaded = false;

    private readonly CancellationSession _loadImageSession = new();
    private bool _reloadPosted = false;
    private bool _reloadPending = false;
    private long _lastReloadTick = 0;
    private Size _availableSize = new(0, 0);
    private int _imageHash = 0;
    private Size _imageSize = new(0, 0);

    private readonly CancellationSession _uriSession = new();
    private string? _uri = null;
    private IImageSource? _source = null;
    private Size? _originalSize = null;

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
            LoadUri();
            RequestReload();
        }
        else
        {
            UnloadImage();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Size result = base.MeasureOverride(availableSize);

        if (availableSize != _availableSize)
        {
            _availableSize = availableSize;
            RequestReload(throttled: true, post: true);
        }

        return result;
    }

    //
    // Loading
    //

    private void LoadUri()
    {
        if (!_isLoaded)
        {
            return;
        }

        string? uri = Uri;
        if (uri == _uri)
        {
            return;
        }

        _uriSession.Next();
        _uri = uri;
        _source = null;
        _originalSize = null;
        UnloadImage();

        if (string.IsNullOrEmpty(uri))
        {
            return;
        }

        CancellationSession.IToken token = _uriSession.Token;

        CoroutineUtils.Run(async () =>
        {
            IImageSource? source = await ResolveImageSource(uri);

            if (source is null || token.IsCancellationRequested)
            {
                return;
            }

            ImageMeta? meta = await ImageLoader.LoadImageMeta(source, new());

            if (meta is null || token.IsCancellationRequested)
            {
                return;
            }

            _source = source;
            _originalSize = new(meta.Width, meta.Height);
            RequestReload();
        });
    }

    private void RequestReload(bool throttled = false, bool post = false)
    {
        if (throttled)
        {
            long elapsed = Environment.TickCount64 - _lastReloadTick;
            if (elapsed < RELOAD_INTERVAL_MS)
            {
                if (_reloadPending)
                {
                    return;
                }

                _reloadPending = true;
                CoroutineUtils.Run(async () =>
                {
                    await Task.Delay((int)(RELOAD_INTERVAL_MS - elapsed));
                    if (_reloadPending)
                    {
                        RequestReload(post: post);
                    }
                });
                return;
            }
        }

        _reloadPending = false;

        if (post)
        {
            PostReload();
        }
        else
        {
            ReloadImage();
        }
    }

    private void PostReload()
    {
        if (_reloadPosted)
        {
            return;
        }

        _reloadPosted = true;
        CoroutineUtils.PostInMainThread(() =>
        {
            _reloadPosted = false;
            ReloadImage();
        });
    }

    private void ReloadImage()
    {
        if (!_isLoaded)
        {
            return;
        }

        IImageSource? source = _source;
        Size? originalSize = _originalSize;
        if (source is null || originalSize is null)
        {
            return;
        }

        double scale = DisplayUtils.GetRasterizationScale(this) * 1.2;
        Size availableSize = new(_availableSize.Width * scale, _availableSize.Height * scale);
        Size newImageSize = CalculateTargetSize(availableSize, originalSize.Value, Stretch);
        if (newImageSize.Width < 1.0 || newImageSize.Height < 1.0)
        {
            return;
        }

        Size imageSize = _imageSize;
        if (imageSize.Width >= 1.0 && imageSize.Height >= 1.0)
        {
            double widthDiff = Math.Abs(newImageSize.Width - imageSize.Width) / imageSize.Width;
            double heightDiff = Math.Abs(newImageSize.Height - imageSize.Height) / imageSize.Height;
            double diff = Math.Max(widthDiff, heightDiff);
            if (diff < 0.05)
            {
                return;
            }
        }

        int newImageHash = HashCode.Combine(source.Uri, newImageSize.Width, newImageSize.Height);

        _loadImageSession.Next();
        _lastReloadTick = Environment.TickCount64;
        _imageHash = newImageHash;
        _imageSize = newImageSize;

        CancellationSession.IToken token = _loadImageSession.Token;
        IImageResultHandler handler = new WeakImageResultHandler(this, newImageHash);

        CoroutineUtils.Run(async () =>
        {
            await ImageLoader.LoadImage(source, new()
            {
                Token = token,
                DecodeWidth = newImageSize.Width,
                DecodeHeight = newImageSize.Height,
                Handler = handler,
            });
        });
    }

    private static Size CalculateTargetSize(Size available, Size origin, Stretch stretch)
    {
        if (available.Width <= 0.0 || available.Height <= 0.0 || origin.Width <= 0.0 || origin.Height <= 0.0)
        {
            return new(0.0, 0.0);
        }

        switch (stretch)
        {
            case Stretch.None:
                return origin;
            case Stretch.Fill:
                return available;
            case Stretch.UniformToFill:
                {
                    double scale = Math.Max(available.Width / origin.Width, available.Height / origin.Height);
                    return new(origin.Width * scale, origin.Height * scale);
                }
            default:
                {
                    double scale = Math.Min(available.Width / origin.Width, available.Height / origin.Height);
                    return new(origin.Width * scale, origin.Height * scale);
                }
        }
    }

    private void UnloadImage()
    {
        _loadImageSession.Next();
        _imageHash = 0;
        _imageSize = new(0, 0);
        ImageHolder.Source = null;
    }

    private class WeakImageResultHandler(SimpleImageView view, int imageHash) : IImageResultHandler
    {
        private readonly WeakReference<SimpleImageView> _imageViewRef = new(view);

        public void OnSuccess(DecodedImageModel result)
        {
            if (!_imageViewRef.TryGetTarget(out SimpleImageView? view) || !view.IsLoaded || view._imageHash != imageHash)
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
