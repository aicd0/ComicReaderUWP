// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ComicReaderUWP.Common.Imaging;

internal sealed partial class ImageSourceHolder : IDisposable
{
    public delegate void SourceChangedHandler(ImageSource? source);
    public event SourceChangedHandler? SourceChanged;

    public ImageSource? Source => _canvasImageSource;

    private ScaleEffect? _scaleEffect;
    public float Scale
    {
        get => _scaleEffect?.Scale.X ?? 1F;
        set
        {
            if (value != Scale)
            {
                _scaleEffect?.Dispose();
                _scaleEffect = new()
                {
                    Scale = new(value),
                    InterpolationMode = CanvasImageInterpolation.HighQualityCubic,
                };
                Draw();
            }
        }
    }

    private CanvasDevice? _canvasDevice;
    private CanvasBitmap? _canvasBitmap;
    private CanvasImageSource? _canvasImageSource;

    public void Dispose()
    {
        _scaleEffect?.Dispose();
        _scaleEffect = null;
        _canvasDevice = null;
        _canvasBitmap?.Dispose();
        _canvasBitmap = null;
        _canvasImageSource = null;
        SourceChanged = null;
    }

    public void SetImage(DecodedImageModel imageModel)
    {
        Image<Bgra32> image = imageModel.Image;
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
        _canvasBitmap?.Dispose();
        _canvasBitmap = canvasBitmap;

        Draw();
    }

    private void Draw()
    {
        CanvasBitmap? canvasBitmap = _canvasBitmap;
        if (canvasBitmap is null)
        {
            return;
        }

        CanvasImageSource? imageSource = GetCanvasImageSource(
            (int)canvasBitmap.SizeInPixels.Width,
            (int)canvasBitmap.SizeInPixels.Height);
        using CanvasDrawingSession ds = imageSource.CreateDrawingSession(Colors.Black);
        ScaleEffect? scaleEffect = _scaleEffect;
        if (scaleEffect is not null)
        {
            scaleEffect.Source = canvasBitmap;
            ds.DrawImage(scaleEffect);
        }
        else
        {
            ds.DrawImage(canvasBitmap);
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
}
