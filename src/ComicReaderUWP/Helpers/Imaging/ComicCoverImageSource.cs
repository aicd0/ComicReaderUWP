// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.IO;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Data.Models.Comic;

namespace ComicReaderUWP.Helpers.Imaging;

internal class ComicCoverImageSource(ComicModel comic) : IImageSource
{
    private readonly ComicModel _comic = comic;

    public string Uri => _comic.CoverImageCacheKey;

    public bool ValidateFingerprint => false;

    public string CalculateFingerprint()
    {
        using ComicConnection? connection = _comic.OpenComic().Result;
        if (connection is null)
        {
            return string.Empty;
        }

        return connection.GetImageSignature(ComicHandle.COVER_INDEX);
    }

    public Stream? OpenImageStream()
    {
        using ComicConnection? connection = _comic.OpenComic().Result;
        if (connection is null)
        {
            return null;
        }

        return connection.OpenImageStream(ComicHandle.COVER_INDEX);
    }

    public IVectorImageService? OpenVectorService()
    {
        using ComicConnection? connection = _comic.OpenComic().Result;
        if (connection is null)
        {
            return null;
        }

        return connection.OpenVectorService(ComicHandle.COVER_INDEX);
    }
}
