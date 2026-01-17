// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.IO;

using Microsoft.Graphics.Canvas;

namespace ComicReaderUWP.Common.Imaging;

internal interface IImageSource
{
    string GetUri();

    string GetContentFingerprint();

    Stream? OpenImageStream();

    CanvasBitmap? CreateImageCanvasBitmap(ICanvasResourceCreator creator);
}
