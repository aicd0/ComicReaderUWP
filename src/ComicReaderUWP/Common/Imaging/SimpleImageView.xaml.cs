// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.SDK.Common.Threading;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Common.Imaging;

internal partial class SimpleImageView : UserControl
{
    private readonly CancellationSession _cancellationSession = new();
    private bool _isLoaded = false;
    private Model? _viewModel;
    private int _currentImageHash = 0;

    public SimpleImageView()
    {
        InitializeComponent();
        Loaded += SimpleImageView_LoadedOrUnloaded;
        Unloaded += SimpleImageView_LoadedOrUnloaded;
    }

    public void SetModel(Model? model)
    {
        _viewModel = model;
        UpdateImage();
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

        Model? viewModel = _viewModel;
        if (viewModel is null)
        {
            UnloadImage();
            return;
        }

        int newHash = viewModel.GetImageHashCode();
        if (newHash == _currentImageHash)
        {
            return;
        }

        UnloadImage();
        _currentImageHash = newHash;
        CancellationSession.IToken token = _cancellationSession.Token;
        IImageResultHandler handler = new WeakImageResultHandler(this);
        viewModel.Dispatcher.Submit(viewModel.DebugDescription, delegate
        {
            LoadImage(token, viewModel, handler);
        });
    }

    private void UnloadImage()
    {
        _currentImageHash = 0;
        _cancellationSession.Next();
        ImageHolder.Source = null;
    }

    private static void LoadImage(CancellationSession.IToken token, Model model, IImageResultHandler handler)
    {
        double width = model.Width * model.Multiplication;
        double height = model.Height * model.Multiplication;
        LoadImageOptions options = new()
        {
            Token = token,
            FrameWidth = width,
            FrameHeight = height,
            StretchMode = model.StretchMode,
            Handler = handler,
        };
        ImageCacheManager.LoadImage(model.Source, options);
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

    public class Model
    {
        public required IImageSource Source { get; set; }
        public required double Width { get; set; } = double.PositiveInfinity;
        public required double Height { get; set; } = double.PositiveInfinity;
        public StretchModeEnum StretchMode { get; set; } = StretchModeEnum.Uniform;
        public double Multiplication { get; set; } = DisplayUtils.GetRawPixelPerPixel();
        public required ITaskDispatcher Dispatcher { get; set; }
        public string DebugDescription { get; set; } = string.Empty;

        public int GetImageHashCode()
        {
            return HashCode.Combine(
                Source.GetHashCode(),
                Width,
                Height,
                StretchMode,
                Multiplication);
        }
    }
}
