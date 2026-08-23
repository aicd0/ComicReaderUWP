// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using Microsoft.Graphics.Canvas;

using Windows.Graphics.Imaging;

namespace ComicReaderUWP.Common.Imaging;

internal interface IVectorImageService : IDisposable
{
    Task<SoftwareBitmap?> CreateSoftwareBitmap(int width, int height);

    Task<CanvasBitmap?> CreateImageCanvasBitmap(ICanvasResourceCreator creator, int width, int height);
}
