// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.IO;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Data.Models.Comic;

using Microsoft.Graphics.Canvas;

namespace ComicReaderUWP.Helpers.Imaging;

internal class ComicImageSource(IComicConnection connection, int index) : IImageSource
{
    private readonly IComicConnection _connection = connection;
    private readonly int _index = index;

    public string GetUri()
    {
        return _connection.GetImageCacheKey(_index);
    }

    public string GetContentFingerprint()
    {
        return _connection.GetImageSignature(_index);
    }

    public Stream? OpenImageStream()
    {
        return _connection.OpenImageStream(_index);
    }

    public CanvasBitmap? CreateImageCanvasBitmap(ICanvasResourceCreator creator)
    {
        return _connection.CreateImageCanvasBitmap(creator, _index);
    }
}
