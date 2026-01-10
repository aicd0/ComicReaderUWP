// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Common.Utils;

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace ComicReaderUWP.UserControls.Reader;

internal partial class ReaderImageSourceHolder : IDisposable
{
    private const int MAX_CANVAS_SIZE = 33177600;

    public delegate void SourceChangedHandler(ImageSource? source);
    public event SourceChangedHandler? SourceChanged;

    public double Scale { get; set; } = double.PositiveInfinity;
    public bool PlaceholderMode { get; set; } = false;
    public double LeftImageWidth { get; set; }
    public double LeftImageHeight { get; set; }
    public double RightImageWidth { get; set; }
    public double RightImageHeight { get; set; }

    public ImageSource? Source => _canvasImageSource;

    private CanvasDevice? _canvasDevice;
    private CanvasImageSource? _canvasImageSource;
    private CanvasBitmap? _leftCanvasBitmap;
    private CanvasBitmap? _rightCanvasBitmap;

    public void Dispose()
    {
        _canvasDevice = null;
        _canvasImageSource = null;
        _leftCanvasBitmap?.Dispose();
        _leftCanvasBitmap = null;
        _rightCanvasBitmap?.Dispose();
        _rightCanvasBitmap = null;
    }

    public void Invalidate()
    {
        Draw();
    }

    public void SetLeftImage(DecodedImageModel? imageModel)
    {
        _leftCanvasBitmap?.Dispose();
        _leftCanvasBitmap = null;

        if (imageModel is not null)
        {
            SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Bgra32> image = imageModel.Image;
            byte[] pixels = new byte[image.Width * image.Height * 4];
            image.CopyPixelDataTo(pixels); // CPU copy

            CanvasDevice device = GetCanvasDevice();
            var canvasBitmap = CanvasBitmap.CreateFromBytes(
                device,
                pixels,
                image.Width,
                image.Height,
                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                96F,
                CanvasAlphaMode.Premultiplied);
            _leftCanvasBitmap = canvasBitmap;
        }

        Draw();
    }

    public void SetRightImage(DecodedImageModel? imageModel)
    {
        _rightCanvasBitmap?.Dispose();
        _rightCanvasBitmap = null;

        if (imageModel is not null)
        {
            SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Bgra32> image = imageModel.Image;
            byte[] pixels = new byte[image.Width * image.Height * 4];
            image.CopyPixelDataTo(pixels); // CPU copy

            CanvasDevice device = GetCanvasDevice();
            var canvasBitmap = CanvasBitmap.CreateFromBytes(
                device,
                pixels,
                image.Width,
                image.Height,
                Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                96F,
                CanvasAlphaMode.Premultiplied);
            _rightCanvasBitmap = canvasBitmap;
        }

        Draw();
    }

    private void Draw()
    {
        CanvasBitmap?[] bitmaps = [
            _leftCanvasBitmap,
            _rightCanvasBitmap
        ];
        FrameSize[] frameSizes = [
            new(LeftImageWidth, LeftImageHeight),
            new(RightImageWidth, RightImageHeight)
        ];

        double maxPixelRatio = 0;
        for (int i = 0; i < bitmaps.Length; i++)
        {
            CanvasBitmap? bitmap = bitmaps[i];
            if (bitmap is null)
            {
                continue;
            }

            if (bitmap.SizeInPixels.Width < 1 || bitmap.SizeInPixels.Height < 1)
            {
                bitmaps[i] = null;
                continue;
            }

            FrameSize frameSize = frameSizes[i];
            if (frameSize.Width < 1E-3 || frameSize.Height < 1E-3)
            {
                bitmaps[i] = null;
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
                SourceChanged?.Invoke(null);
            }

            return;
        }

        double finalPixelRatio = Math.Min(Scale * DisplayUtils.GetRawPixelPerPixel(), maxPixelRatio);
        double accumulatedWidth = 0;
        double maxHeight = 0;
        for (int i = 0; i < bitmaps.Length; i++)
        {
            CanvasBitmap? bitmap = bitmaps[i];
            if (bitmap is null && !PlaceholderMode)
            {
                continue;
            }

            FrameSize frameSize = frameSizes[i];
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
            CanvasBitmap? bitmap = bitmaps[i];
            if (bitmap is null && !PlaceholderMode)
            {
                continue;
            }

            ImageRect imageRect = new();
            FrameSize frameSize = frameSizes[i];
            double rectWidth = frameSize.Width * finalPixelRatio * scaleRatio;
            imageRect.X = (int)accumulatedWidth;
            imageRect.Width = rectWidth;
            accumulatedWidth += rectWidth;

            if (bitmap is not null)
            {
                double rectHeight = rectWidth * scaleRatio * bitmap.SizeInPixels.Height / bitmap.SizeInPixels.Width;
                double rectTop = (maxHeight - rectHeight) * 0.5;
                imageRect.Y = rectTop;
                imageRect.Height = rectHeight;
            }

            imageRects[i] = imageRect;
        }

        CanvasImageSource? imageSource = GetCanvasImageSource((int)accumulatedWidth, (int)maxHeight);
        using CanvasDrawingSession ds = imageSource.CreateDrawingSession(Colors.Transparent);
        for (int i = 0; i < bitmaps.Length; i++)
        {
            CanvasBitmap? bitmap = bitmaps[i];
            if (bitmap is null)
            {
                continue;
            }

            ImageRect imageRect = imageRects[i];
            ds.DrawImage(
                bitmap,
                new Windows.Foundation.Rect(imageRect.X, imageRect.Y, imageRect.Width, imageRect.Height),
                new Windows.Foundation.Rect(0, 0, bitmap.SizeInPixels.Width, bitmap.SizeInPixels.Height),
                1F,
                CanvasImageInterpolation.HighQualityCubic);
        }
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

    private struct FrameSize
    {
        public double Width;
        public double Height;

        public FrameSize(double width, double height)
        {
            Width = width;
            Height = height;
        }
    }

    private struct ImageRect
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;
    }
}
