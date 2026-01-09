// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;

using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.SDK.Common.Threading;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Common.Imaging;

internal partial class SimpleImageView : UserControl
{
    private bool _isLoaded;
    private Model _model;
    private int _currentImageHash = 0;
    private readonly CancellationSession _cancellationSession = new();

    public SimpleImageView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void SetModel(Model model)
    {
        _model = model;
        UpdateImage();
    }

    public void UnsetModel()
    {
        _model = null;
        UpdateImage();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        UpdateImage();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        UpdateImage();
    }

    private void UpdateImage()
    {
        Model model = _model;
        if (!_isLoaded || model == null)
        {
            UnloadImage();
            return;
        }

        int newHash = model.GetImageHashCode();
        if (newHash == _currentImageHash)
        {
            return;
        }

        UnloadImage();
        _currentImageHash = newHash;

        CancellationSession.IToken token = _cancellationSession.Token;
        IImageResultHandler handler = new WeakImageResultHandler(this, model.Callback);
        model.Dispatcher.Submit(model.DebugDescription, delegate
        {
            LoadImage(token, model, handler);
        });
    }

    private void UnloadImage()
    {
        _currentImageHash = 0;
        _cancellationSession.Next();

        if (ImageHolder.Source != null)
        {
            ImageHolder.Source = null;
        }
    }

    private static void LoadImage(CancellationSession.IToken token, Model model, IImageResultHandler handler)
    {
        double width = model.Width * model.Multiplication;
        double height = model.Height * model.Multiplication;
        ImageCacheManager.LoadImage(token, model.Source, width,
            height, model.StretchMode, handler);
    }

    private class WeakImageResultHandler : IImageResultHandler
    {
        private readonly WeakReference<SimpleImageView> _imageView;
        private readonly IImageCallback _callback;

        public WeakImageResultHandler(SimpleImageView view, IImageCallback callback)
        {
            _imageView = new WeakReference<SimpleImageView>(view);
            _callback = callback;
        }

        public void OnSuccess(DecodedImageModel result)
        {
            if (_imageView.TryGetTarget(out SimpleImageView view))
            {
                view.ImageHolder.Source = ImagingUtils.CreateImageSource(result);
                _callback?.OnSuccess(result);
            }
        }

        public void OnFailure()
        {
        }
    }

    public interface IImageCallback
    {
        void OnSuccess(DecodedImageModel image);
    }

    public class Model
    {
        public IImageSource Source { get; set; }
        public double Width { get; set; } = double.PositiveInfinity;
        public double Height { get; set; } = double.PositiveInfinity;
        public StretchModeEnum StretchMode { get; set; } = StretchModeEnum.Uniform;
        public double Multiplication { get; set; } = 1.0;
        public ITaskDispatcher Dispatcher { get; set; }
        public IImageCallback Callback { get; set; }
        public string DebugDescription { get; set; }

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
