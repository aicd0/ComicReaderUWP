// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Drawing;
using System.Numerics;

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI.Composition;
using Microsoft.UI.Composition;

namespace ComicReaderUWP.UserControls.Reader.Imaging;

internal class ReaderImageUpdateScheduler
{
    public static ReaderImageUpdateScheduler Instance { get; } = new();

    private readonly Dictionary<int, CompositionGroupModel> _groups = [];

    private ReaderImageUpdateScheduler()
    {
    }

    public void AddGroup(CompositionGroupModel group)
    {
        if (_groups.TryAdd(group.Id, group))
        {
            DrawGroup(group);
        }
    }

    public void RemoveGroup(CompositionGroupModel group)
    {
        _groups.Remove(group.Id);
    }

    private void DrawGroup(CompositionGroupModel group)
    {
        if (!group.SurfaceRef.TryRef(out CompositionDrawingSurface? surface))
        {
            _groups.Remove(group.Id);
            return;
        }

        try
        {
            using CanvasDrawingSession ds = CanvasComposition.CreateDrawingSession(surface);
            ds.Clear(Microsoft.UI.Colors.Transparent);

            foreach (CompositionItemModel item in group.Items)
            {
                if (!item.BitmapRef.TryRef(out CanvasBitmap? bitmap))
                {
                    continue;
                }

                try
                {
                    Matrix3x2 oldTransform = ds.Transform;
                    ds.Transform = item.GetTransformMatrix(item.CanvasRect, out RectangleF destRect);
                    ds.DrawImage(
                        CreateCanvasImage(bitmap, item.ImageSource),
                        new Windows.Foundation.Rect(destRect.X, destRect.Y, destRect.Width, destRect.Height),
                        new Windows.Foundation.Rect(0, 0, bitmap.SizeInPixels.Width, bitmap.SizeInPixels.Height),
                        1F,
                        CanvasImageInterpolation.HighQualityCubic);
                    ds.Transform = oldTransform;
                }
                finally
                {
                    item.BitmapRef.Unref();
                }
            }
        }
        finally
        {
            group.SurfaceRef.Unref();
        }
    }

    private static ICanvasImage CreateCanvasImage(CanvasBitmap bitmap, ReaderImageSource source)
    {
        if (source.Invert)
        {
            return new InvertEffect()
            {
                Source = bitmap,
            };
        }

        return bitmap;
    }
}
