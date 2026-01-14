// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;

using Microsoft.Graphics.Canvas;

namespace ComicReaderUWP.Data.Models.Comic;

internal interface IComicConnection : IDisposable
{
    int GetImageCount();

    string GetImageName(int index);

    string GetImageCacheKey(int index);

    string GetImageSignature(int index);

    Stream? OpenImageStream(int index);

    CanvasBitmap? CreateImageCanvasBitmap(ICanvasResourceCreator creator, int index);
}
