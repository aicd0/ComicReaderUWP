// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ComicReaderUWP.Common.Imaging;

internal static class ImagingUtils
{
    public static ImageSource CreateImageSource(DecodedImageModel model)
    {
        Image<Bgra32> image = model.Image;
        byte[] pixels = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(pixels);
        var device = CanvasDevice.GetSharedDevice();
        using var canvasBitmap = CanvasBitmap.CreateFromBytes(
            device,
            pixels,
            image.Width,
            image.Height,
            Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
            96F,
            CanvasAlphaMode.Premultiplied);
        var imageSource = new CanvasImageSource(device, image.Width, image.Height, 96F);
        using (CanvasDrawingSession ds = imageSource.CreateDrawingSession(Colors.Black))
        {
            ds.DrawImage(canvasBitmap);
        }

        return imageSource;
    }
}
