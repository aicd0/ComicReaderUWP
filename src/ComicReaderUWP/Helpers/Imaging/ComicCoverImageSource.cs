// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.IO;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Data.Models.Comic;

namespace ComicReaderUWP.Helpers.Imaging;

internal class ComicCoverImageSource(ComicModel comic) : IImageSource
{
    private readonly ComicModel _comic = comic;

    public string GetUri()
    {
        return _comic.CoverImageCacheKey;
    }

    public string GetContentFingerprint()
    {
        return string.Empty;
    }

    public Stream? OpenImageStream()
    {
        using IComicConnection? connection = _comic.OpenComicAsync().Result;
        if (connection == null)
        {
            return null;
        }

        return connection.OpenImageStream(0);
    }
}
