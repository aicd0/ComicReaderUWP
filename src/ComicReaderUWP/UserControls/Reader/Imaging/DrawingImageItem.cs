// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Drawing;
using System.Numerics;

using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Data.Models.Misc;

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;

using Windows.Graphics.Imaging;

namespace ComicReaderUWP.UserControls.Reader.Imaging;

internal class DrawingImageItem
{
    public required ReaderImageSource Source;
    public required RefCounted<CanvasBitmap> Bitmap;
    public required BitmapSize BitmapSize;
    public RectangleF TargetRect;

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

    public Matrix3x2 GetTransformMatrix(RectangleF imageRect, out RectangleF destRect)
    {
        Matrix3x2 transform = Matrix3x2.Identity;

        var center = new Vector2(
            imageRect.X + imageRect.Width / 2.0F,
            imageRect.Y + imageRect.Height / 2.0F);

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
                destRect = new RectangleF
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
