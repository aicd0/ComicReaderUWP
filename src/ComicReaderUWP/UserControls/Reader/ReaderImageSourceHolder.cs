// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Threading;
using ComicReaderUWP.SDK.Common.Utils;

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace ComicReaderUWP.UserControls.Reader;

internal partial class ReaderImageSourceHolder(ITaskDispatcher dispatcher) : IDisposable
{
    private const string TAG = nameof(ReaderImageSourceHolder);
    private const int MAX_CANVAS_SIZE = 33177600;

    public delegate void SourceChangedHandler(ImageSource? source);
    public event SourceChangedHandler? SourceChanged;

    private double _scale = double.PositiveInfinity;
    public double Scale
    {
        get => _scale;
        set
        {
            if (double.IsNaN(value) || value <= 0.0)
            {
                return;
            }

            double fixedValue = value * DisplayUtils.GetRawPixelPerPixel() * 1.2;
            if (_scale == fixedValue)
            {
                return;
            }

            _scale = fixedValue;
            InvalidateVectorImages();
            PostDrawTask();
        }
    }

    public bool PlaceholderMode { get; set; } = false;
    public ImageSource? Source => _canvasImageSource;

    private readonly ITaskDispatcher _decodeDispatcher = dispatcher;
    private readonly ITaskDispatcher _drawDispatcher = TaskDispatcher.DefaultThreadPool;
    private readonly List<ImageItem> _images = [];
    private CanvasDevice? _canvasDevice;
    private CanvasImageSource? _canvasImageSource;
    private int _postDraw = 0;

    public void Dispose()
    {
        _canvasDevice = null;
        _canvasImageSource = null;

        foreach (ImageItem item in _images)
        {
            item.Dispose();
        }

        _images.Clear();
    }

    public void Invalidate()
    {
        PostDrawTask();
    }

    public void SetImage(int index, IImageSource? source, double frameWidth, double frameHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));

        ImageItem item;
        bool needDraw = false;
        lock (_images)
        {
            while (index >= _images.Count)
            {
                needDraw = true;
                _images.Add(new());
            }

            item = _images[index];
        }

        bool supportVector = false;
        if (source is not null)
        {
            using IVectorImageService? vectorService = source.OpenVectorService();
            supportVector = vectorService is not null;
        }

        bool postLoading;
        lock (item.Lock)
        {
            if (item.Source != source || item.FrameSize.Width != frameWidth || item.FrameSize.Height != frameHeight)
            {
                needDraw = true;
            }

            if (!needDraw)
            {
                return;
            }

            item.Source = source;
            item.FrameSize = new(frameWidth, frameHeight);
            item.SupportVector = supportVector;

            if (item.IsLoading)
            {
                item.IsLoadInvalidated = true;
            }

            postLoading = !item.IsLoading;
            item.IsLoading = true;
            item.ClearPrevious = true;
        }

        if (!postLoading)
        {
            return;
        }

        PostDecodeTask(item);
    }

    private void InvalidateVectorImages()
    {
        List<ImageItem> vectorItems = [];
        lock (_images)
        {
            foreach (ImageItem item in _images)
            {
                lock (item.Lock)
                {
                    if (!item.SupportVector)
                    {
                        continue;
                    }

                    if (item.IsLoading)
                    {
                        item.IsLoadInvalidated = true;
                    }
                    else
                    {
                        vectorItems.Add(item);
                        item.IsLoading = true;
                    }
                }
            }
        }

        if (vectorItems.Count == 0)
        {
            return;
        }

        foreach (ImageItem item in vectorItems)
        {
            PostDecodeTask(item);
        }
    }

    private void PostDecodeTask(ImageItem item)
    {
        _decodeDispatcher.Submit(() =>
        {
            bool loadInvalidated = false;
            do
            {
                try
                {
                    DecodeImage(item);
                }
                catch (Exception)
                {
                    lock (item.Lock)
                    {
                        item.IsLoading = false;
                    }

                    throw;
                }

                lock (item.Lock)
                {
                    loadInvalidated = item.IsLoadInvalidated;
                    item.IsLoadInvalidated = false;
                    item.IsLoading = loadInvalidated;
                }
            } while (loadInvalidated);
        });
    }

    private void DecodeImage(ImageItem item)
    {
        IImageSource? source;
        bool clearPrevious;
        Size frameSize;
        lock (item.Lock)
        {
            source = item.Source;
            clearPrevious = item.ClearPrevious;
            item.ClearPrevious = false;
            frameSize = item.FrameSize;
        }

        if (clearPrevious)
        {
            bool needDraw;
            lock (item.Lock)
            {
                needDraw = item.BitmapRef is not null;
                item.BitmapRef?.Unref();
                item.BitmapRef = null;
            }

            if (needDraw)
            {
                PostDrawTask();
            }
        }

        if (source is null)
        {
            return;
        }

        CanvasBitmap? newBitmap;
        using IVectorImageService? vectorService = source.OpenVectorService();
        if (vectorService is not null)
        {
            double width = frameSize.Width * _scale;
            double height = frameSize.Height * _scale;
            double resolution = width * height;

            if (resolution < 1E-2)
            {
                return;
            }

            const double maxResolution = 10000000;
            if (resolution > maxResolution)
            {
                double ratio = Math.Sqrt(maxResolution / (frameSize.Width * frameSize.Height));
                width = frameSize.Width * ratio;
                height = frameSize.Height * ratio;
            }

            CanvasDevice device = GetCanvasDevice();
            newBitmap = vectorService.CreateImageCanvasBitmap(device,
                (int)Math.Round(width), (int)Math.Round(height));
        }
        else
        {
            using Stream? stream = source.OpenImageStream();
            if (stream is null)
            {
                return;
            }

            CanvasDevice device = GetCanvasDevice();
            try
            {
                newBitmap = CanvasBitmap.LoadAsync(device, stream.AsRandomAccessStream()).AsTask().Result;
            }
            catch (Exception e)
            {
                Logger.E(TAG, e);
                return;
            }
        }

        if (newBitmap is null)
        {
            return;
        }

        RefCounted<CanvasBitmap>? newBitmapRef = new(newBitmap);
        RefCounted<CanvasBitmap>? oldBitmapRef;
        lock (item.Lock)
        {
            oldBitmapRef = item.BitmapRef;
            item.BitmapRef = newBitmapRef;
        }

        oldBitmapRef?.Unref();
        PostDrawTask();
    }

    private void PostDrawTask()
    {
        if (Interlocked.Exchange(ref _postDraw, 1) == 1)
        {
            return;
        }

        _drawDispatcher.Submit(() =>
        {
            Interlocked.Exchange(ref _postDraw, 0);
            Draw();
        });
    }

    private void Draw()
    {
        RefCounted<CanvasBitmap>?[] bitmaps;
        Size[] frameSizes;
        lock (_images)
        {
            bitmaps = new RefCounted<CanvasBitmap>?[_images.Count];
            frameSizes = new Size[_images.Count];
            for (int i = 0; i < _images.Count; i++)
            {
                ImageItem item = _images[i];
                lock (item.Lock)
                {
                    item.BitmapRef?.Ref();
                    bitmaps[i] = item.BitmapRef;
                    frameSizes[i] = item.FrameSize;
                }
            }
        }

        double maxPixelRatio = 0;
        for (int i = 0; i < bitmaps.Length; i++)
        {
            RefCounted<CanvasBitmap>? bitmapRef = bitmaps[i];
            if (bitmapRef is null)
            {
                continue;
            }

            CanvasBitmap bitmap = bitmapRef.Value;
            if (bitmap.SizeInPixels.Width < 1 || bitmap.SizeInPixels.Height < 1)
            {
                bitmaps[i] = null;
                bitmapRef.Unref();
                continue;
            }

            Size frameSize = frameSizes[i];
            if (frameSize.Width < 1E-3 || frameSize.Height < 1E-3)
            {
                bitmaps[i] = null;
                bitmapRef.Unref();
                continue;
            }

            double pixelRatio = bitmap.SizeInPixels.Width / frameSize.Width;
            maxPixelRatio = Math.Max(maxPixelRatio, pixelRatio);
        }

        if (maxPixelRatio < 1E-3)
        {
            if (_canvasImageSource is not null)
            {
                _canvasImageSource = null;
                CoroutineUtils.RunInMainThread(() =>
                {
                    if (_canvasImageSource is null)
                    {
                        SourceChanged?.Invoke(null);
                    }
                });
            }

            return;
        }

        double finalPixelRatio = Math.Min(_scale, maxPixelRatio);
        double accumulatedWidth = 0;
        double maxHeight = 0;
        for (int i = 0; i < bitmaps.Length; i++)
        {
            CanvasBitmap? bitmap = bitmaps[i]?.Value;
            if (bitmap is null && !PlaceholderMode)
            {
                continue;
            }

            Size frameSize = frameSizes[i];
            double rectWidth = frameSize.Width * finalPixelRatio;
            accumulatedWidth += rectWidth;

            if (bitmap is not null)
            {
                double rectHeight = rectWidth * bitmap.SizeInPixels.Height / bitmap.SizeInPixels.Width;
                maxHeight = Math.Max(maxHeight, rectHeight);
            }
        }

        double canvasSize = (double)accumulatedWidth * maxHeight;
        double scaleRatio = 1;
        if (canvasSize > MAX_CANVAS_SIZE)
        {
            scaleRatio = Math.Sqrt(MAX_CANVAS_SIZE / canvasSize);
        }

        var imageRects = new ImageRect[bitmaps.Length];
        accumulatedWidth = 0;
        maxHeight *= scaleRatio;
        for (int i = 0; i < bitmaps.Length; i++)
        {
            CanvasBitmap? bitmap = bitmaps[i]?.Value;
            if (bitmap is null && !PlaceholderMode)
            {
                continue;
            }

            ImageRect imageRect = new();
            Size frameSize = frameSizes[i];
            double rectWidth = frameSize.Width * finalPixelRatio * scaleRatio;
            imageRect.X = (int)accumulatedWidth;
            imageRect.Width = rectWidth;
            accumulatedWidth += rectWidth;

            if (bitmap is not null)
            {
                double rectHeight = rectWidth * bitmap.SizeInPixels.Height / bitmap.SizeInPixels.Width;
                double rectTop = (maxHeight - rectHeight) * 0.5;
                imageRect.Y = rectTop;
                imageRect.Height = rectHeight;
            }

            imageRects[i] = imageRect;
        }

        CoroutineUtils.RunInMainThread(() =>
        {
            CanvasImageSource imageSource = GetCanvasImageSource((int)accumulatedWidth, (int)maxHeight);
            using CanvasDrawingSession ds = imageSource.CreateDrawingSession(Colors.Transparent);
            for (int i = 0; i < bitmaps.Length; i++)
            {
                RefCounted<CanvasBitmap>? bitmapRef = bitmaps[i];
                if (bitmapRef is null)
                {
                    continue;
                }

                CanvasBitmap bitmap = bitmapRef.Value;
                ImageRect imageRect = imageRects[i];
                ds.DrawImage(
                    bitmap,
                    new Windows.Foundation.Rect(imageRect.X, imageRect.Y, imageRect.Width, imageRect.Height),
                    new Windows.Foundation.Rect(0, 0, bitmap.SizeInPixels.Width, bitmap.SizeInPixels.Height),
                    1F,
                    CanvasImageInterpolation.HighQualityCubic);
                bitmapRef.Unref();
            }
        });
    }

    private CanvasImageSource GetCanvasImageSource(int width, int height)
    {
        CanvasImageSource? canvasImageSource = _canvasImageSource;
        if (canvasImageSource is not null &&
            canvasImageSource.SizeInPixels.Width == width &&
            canvasImageSource.SizeInPixels.Height == height)
        {
            return canvasImageSource;
        }

        CanvasDevice device = GetCanvasDevice();
        var imageSource = new CanvasImageSource(device, width, height, 96F);
        _canvasImageSource = imageSource;
        SourceChanged?.Invoke(imageSource);
        return imageSource;
    }

    private CanvasDevice GetCanvasDevice()
    {
        CanvasDevice? device = _canvasDevice;
        if (device is not null)
        {
            return device;
        }

        device = CanvasDevice.GetSharedDevice();
        _canvasDevice = device;
        return device;
    }

    private struct Size(double width, double height)
    {
        public double Width = width;
        public double Height = height;
    }

    private struct ImageRect
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;
    }

    private partial class ImageItem : IDisposable
    {
        public object Lock { get; } = new();
        public IImageSource? Source { get; set; }
        public Size FrameSize { get; set; }
        public bool SupportVector { get; set; } = false;
        public bool IsLoading { get; set; } = false;
        public bool IsLoadInvalidated { get; set; } = false;
        public bool ClearPrevious { get; set; } = false;
        public RefCounted<CanvasBitmap>? BitmapRef { get; set; }

        public void Dispose()
        {
            BitmapRef?.Unref();
            BitmapRef = null;
            Source = null;
        }
    }
}
