// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Threading;
using ComicReaderUWP.SDK.Common.Utils;

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

using Windows.Graphics.Imaging;

namespace ComicReaderUWP.UserControls.Reader;

internal partial class ReaderImageSourceHolder(ITaskDispatcher dispatcher) : IDisposable
{
    private const string TAG = nameof(ReaderImageSourceHolder);
    private const int MAX_BITMAP_SIZE = 16 * 1024 * 1024;
    private const int MAX_CANVAS_DIMENSION = 8192;

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
    private int _drawVersion = 0;

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

    public void SetImage(int index, ReaderImageSource? source, double frameWidth, double frameHeight)
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
            using IVectorImageService? vectorService = source.Source.OpenVectorService();
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
        ReaderImageSource? source;
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
        using IVectorImageService? vectorService = source.Source.OpenVectorService();
        if (vectorService is not null)
        {
            double width = frameSize.Width * _scale;
            double height = frameSize.Height * _scale;
            double resolution = width * height;

            if (resolution < 1E-2)
            {
                return;
            }

            const double maxResolution = MAX_BITMAP_SIZE;
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
            using Stream? stream = source.Source.OpenImageStream();
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
            int version = Interlocked.Increment(ref _drawVersion);
            Interlocked.Exchange(ref _postDraw, 0);
            Draw(version);
        });
    }

    private void Draw(int version)
    {
        DrawingImageItem?[] items;
        Size[] frameSizes;
        lock (_images)
        {
            items = new DrawingImageItem[_images.Count];
            frameSizes = new Size[_images.Count];
            for (int i = 0; i < _images.Count; i++)
            {
                ImageItem item = _images[i];
                lock (item.Lock)
                {
                    if (item.BitmapRef is null || item.Source is null)
                    {
                        items[i] = null;
                    }
                    else
                    {
                        items[i] = new DrawingImageItem
                        {
                            Source = item.Source,
                            Bitmap = item.BitmapRef,
                            BitmapSize = item.BitmapRef.Value.SizeInPixels,
                        };
                    }

                    frameSizes[i] = item.FrameSize;
                }
            }
        }

        double maxPixelRatio = 0;
        for (int i = 0; i < items.Length; i++)
        {
            DrawingImageItem? item = items[i];
            if (item is null)
            {
                continue;
            }

            if (item.BitmapSize.Width < 1 || item.BitmapSize.Height < 1)
            {
                items[i] = null;
                continue;
            }

            Size frameSize = frameSizes[i];
            if (frameSize.Width < 1E-3 || frameSize.Height < 1E-3)
            {
                items[i] = null;
                continue;
            }

            double pixelRatio = item.ImageWidth / frameSize.Width;
            maxPixelRatio = Math.Max(maxPixelRatio, pixelRatio);
        }

        if (maxPixelRatio < 1E-3)
        {
            if (_canvasImageSource is not null)
            {
                _canvasImageSource = null;
                CoroutineUtils.RunInMainThread(() =>
                {
                    if (Volatile.Read(ref _drawVersion) != version)
                    {
                        return;
                    }

                    if (_canvasImageSource is null)
                    {
                        SourceChanged?.Invoke(null);
                    }
                });
            }

            return;
        }

        int canvasWidth;
        int canvasHeight;
        {
            double finalPixelRatio = Math.Min(_scale, maxPixelRatio);
            double accumulatedWidth = 0;
            double maxHeight = 0;
            for (int i = 0; i < items.Length; i++)
            {
                DrawingImageItem? item = items[i];
                if (item is null && !PlaceholderMode)
                {
                    continue;
                }

                Size frameSize = frameSizes[i];
                double rectWidth = frameSize.Width * finalPixelRatio;
                accumulatedWidth += rectWidth;

                if (item is not null)
                {
                    double rectHeight = rectWidth * item.ImageHeight / item.ImageWidth;
                    maxHeight = Math.Max(maxHeight, rectHeight);
                }
            }

            int maxDimension = MAX_CANVAS_DIMENSION;
            double scaleRatio = Math.Min(1, maxDimension / Math.Max(accumulatedWidth, maxHeight));
            accumulatedWidth *= scaleRatio;
            maxHeight *= scaleRatio;

            accumulatedWidth = 0;
            double roundedAccumulatedWidth = 0;
            for (int i = 0; i < items.Length; i++)
            {
                DrawingImageItem? item = items[i];
                if (item is null && !PlaceholderMode)
                {
                    continue;
                }

                ImageRect imageRect = new();
                Size frameSize = frameSizes[i];
                double rectWidth = frameSize.Width * finalPixelRatio * scaleRatio;

                // Round left and right sides to the nearest pixel to prevent gaps
                imageRect.X = roundedAccumulatedWidth;
                accumulatedWidth += rectWidth;
                double newRoundedAccumulatedWidth = Math.Round(accumulatedWidth);
                imageRect.Width = Math.Max(1.0, newRoundedAccumulatedWidth - roundedAccumulatedWidth);
                roundedAccumulatedWidth = newRoundedAccumulatedWidth;

                if (item is not null)
                {
                    double rectHeight = rectWidth * item.ImageHeight / item.ImageWidth;
                    double rectTop = (maxHeight - rectHeight) * 0.5;
                    imageRect.Y = rectTop;
                    imageRect.Height = rectHeight;
                    item.TargetRect = imageRect;
                }
            }

            canvasWidth = Math.Max(1, Math.Min(maxDimension, (int)roundedAccumulatedWidth));
            canvasHeight = Math.Max(1, Math.Min(maxDimension, (int)Math.Ceiling(maxHeight)));
        }

        CoroutineUtils.RunInMainThread(() =>
        {
            if (Volatile.Read(ref _drawVersion) != version)
            {
                return;
            }

            CanvasImageSource imageSource = GetCanvasImageSource(canvasWidth, canvasHeight);
            using CanvasDrawingSession ds = imageSource.CreateDrawingSession(Colors.Transparent);
            for (int i = 0; i < items.Length; i++)
            {
                DrawingImageItem? item = items[i];
                if (item is null)
                {
                    continue;
                }

                if (!item.Bitmap.TryRef(out CanvasBitmap? bitmap))
                {
                    continue;
                }

                try
                {
                    Matrix3x2 oldTransform = ds.Transform;
                    ds.Transform = item.GetTransformMatrix(item.TargetRect, out ImageRect destRect);
                    ds.DrawImage(
                        item.CanvasImage,
                        new Windows.Foundation.Rect((float)destRect.X, (float)destRect.Y, (float)destRect.Width, (float)destRect.Height),
                        new Windows.Foundation.Rect(0, 0, item.BitmapSize.Width, item.BitmapSize.Height),
                        1F,
                        CanvasImageInterpolation.HighQualityCubic);
                    ds.Transform = oldTransform;
                }
                finally
                {
                    item.Bitmap.Unref();
                }
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

    private class DrawingImageItem
    {
        public required ReaderImageSource Source;
        public required RefCounted<CanvasBitmap> Bitmap;
        public required BitmapSize BitmapSize;
        public ImageRect TargetRect;

        public ICanvasImage CanvasImage
        {
            get
            {
                if (Source.Invert)
                {
                    return new InvertEffect()
                    {
                        Source = Bitmap.Value,
                    };
                }

                return Bitmap.Value;
            }
        }

        public uint ImageWidth => Source.Rotation switch
        {
            ImageRotationEnum.Rotate90 or ImageRotationEnum.Rotate270 => BitmapSize.Height,
            _ => Bitmap.Value.SizeInPixels.Width,
        };

        public uint ImageHeight => Source.Rotation switch
        {
            ImageRotationEnum.Rotate90 or ImageRotationEnum.Rotate270 => BitmapSize.Width,
            _ => Bitmap.Value.SizeInPixels.Height,
        };

        public Matrix3x2 GetTransformMatrix(ImageRect imageRect, out ImageRect destRect)
        {
            Matrix3x2 transform = Matrix3x2.Identity;

            var center = new Vector2(
                (float)(imageRect.X + imageRect.Width / 2.0),
                (float)(imageRect.Y + imageRect.Height / 2.0));

            if (Source.Flip)
            {
                transform *= Matrix3x2.CreateScale(-1, 1, center);
            }

            switch (Source.Rotation)
            {
                case ImageRotationEnum.Rotate90:
                    transform *= Matrix3x2.CreateRotation(MathF.PI / 2, center);
                    break;
                case ImageRotationEnum.Rotate180:
                    transform *= Matrix3x2.CreateRotation(MathF.PI, center);
                    break;
                case ImageRotationEnum.Rotate270:
                    transform *= Matrix3x2.CreateRotation(MathF.PI * 3 / 2, center);
                    break;
                default:
                    break;
            }

            switch (Source.Rotation)
            {
                case ImageRotationEnum.Rotate90 or ImageRotationEnum.Rotate270:
                    var originTransform = Matrix3x2.CreateRotation(-MathF.PI / 2, center);
                    var p0 = Vector2.Transform(new Vector2((float)(imageRect.X + imageRect.Width), (float)imageRect.Y), originTransform);
                    destRect = new ImageRect
                    {
                        X = p0.X,
                        Y = p0.Y,
                        Width = imageRect.Height,
                        Height = imageRect.Width,
                    };
                    break;
                default:
                    destRect = imageRect;
                    break;
            }

            return transform;
        }
    }

    private partial class ImageItem : IDisposable
    {
        public object Lock { get; } = new();
        public ReaderImageSource? Source { get; set; }
        public Size FrameSize { get; set; }
        public bool SupportVector { get; set; } = false;
        public bool IsLoading { get; set; } = false;
        public bool IsLoadInvalidated { get; set; } = false;
        public bool ClearPrevious { get; set; } = false;
        public RefCounted<CanvasBitmap>? BitmapRef { get; set; }

        public void Dispose()
        {
            lock (Lock)
            {
                BitmapRef?.Unref();
                BitmapRef = null;
                Source = null;
            }
        }
    }
}
