// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Drawing;

using Microsoft.Graphics.Canvas;

using Windows.Graphics.Imaging;

namespace ComicReaderUWP.Common.Imaging;

internal interface IVectorImageService : IDisposable
{
    SizeF Size { get; }

    SoftwareBitmap? CreateSoftwareBitmap(int width, int height);

    CanvasBitmap? CreateImageCanvasBitmap(ICanvasResourceCreator creator, int width, int height);
}
