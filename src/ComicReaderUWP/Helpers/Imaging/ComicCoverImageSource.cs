// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Data.Models.Comic;

namespace ComicReaderUWP.Helpers.Imaging;

internal class ComicCoverImageSource : IImageSource
{
    public static async Task<ComicCoverImageSource> Create(ComicModel comic)
    {
        return new ComicCoverImageSource(comic, await comic.GetCoverImageCacheKey());
    }

    private readonly ComicModel _comic;
    private readonly string _uri;

    public string Uri => _uri;

    public bool ValidateFingerprint => false;

    private ComicCoverImageSource(ComicModel comic, string uri)
    {
        _comic = comic;
        _uri = uri;
    }

    public async Task<string> GetFingerprint()
    {
        using ComicConnection? connection = await _comic.OpenComic();
        if (connection is null)
        {
            return string.Empty;
        }

        return connection.GetImageSignature(ComicHandle.COVER_INDEX);
    }

    public async Task<Stream?> OpenImageStream()
    {
        using ComicConnection? connection = await _comic.OpenComic();
        if (connection is null)
        {
            return null;
        }

        return await connection.OpenImageStream(ComicHandle.COVER_INDEX);
    }

    public async Task<IVectorImageService?> OpenVectorService()
    {
        using ComicConnection? connection = await _comic.OpenComic();
        if (connection is null)
        {
            return null;
        }

        return connection.OpenVectorService(ComicHandle.COVER_INDEX);
    }
}
