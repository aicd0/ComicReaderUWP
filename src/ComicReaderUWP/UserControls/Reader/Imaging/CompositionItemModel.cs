// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Drawing;
using System.Numerics;
using System.Threading;

using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Data.Models.Misc;

namespace ComicReaderUWP.UserControls.Reader.Imaging;

internal class CompositionItemModel
{
    private static int _idCounter = 0;

    public int Id { get; } = Interlocked.Increment(ref _idCounter);
    public required RefCounted<IReaderBitmapModelForGPU> BitmapRef { get; init; }
    public required ReaderImageSource Source { get; init; }
    public required RectangleF CanvasRect { get; init; }

    public Matrix3x2 GetTransformMatrix(RectangleF imageRect, out RectangleF destRect)
    {
        Matrix3x2 transform = Matrix3x2.Identity;

        var center = new Vector2(
            imageRect.X + imageRect.Width / 2.0F,
            imageRect.Y + imageRect.Height / 2.0F);

        if (Source.Settings.Flip)
        {
            transform *= Matrix3x2.CreateScale(-1, 1, center);
        }

        switch (Source.Settings.Rotation)
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

        switch (Source.Settings.Rotation)
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
