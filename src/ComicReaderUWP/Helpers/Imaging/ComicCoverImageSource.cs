// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Data.Models.Comic;

namespace ComicReaderUWP.Helpers.Imaging;

internal sealed partial class ComicCoverImageSource : IImageSource
{
    public static async Task<ComicCoverImageSource> Create(ComicModel comic)
    {
        string coverCacheKey = comic.CoverCacheKey;
        if (string.IsNullOrEmpty(coverCacheKey))
        {
            using ComicConnection? connection = await comic.OpenComic();
            coverCacheKey = comic.CoverCacheKey;
        }

        var preferredSchedulerGroup = ImageLoaderSchedulerGroup.FromPath(comic.Location);
        return new ComicCoverImageSource(comic, coverCacheKey, preferredSchedulerGroup);
    }

    private readonly ComicModel _comic;
    private readonly string _uri;
    private readonly ImageLoaderSchedulerGroup _preferredSchedulerGroup;

    public string Uri => _uri;

    public ImageLoaderSchedulerGroup PreferredSchedulerGroup => _preferredSchedulerGroup;

    public bool ValidateFingerprint => false;

    private ComicCoverImageSource(ComicModel comic, string uri, ImageLoaderSchedulerGroup preferredSchedulerGroup)
    {
        _comic = comic;
        _uri = uri;
        _preferredSchedulerGroup = preferredSchedulerGroup;
    }

    public async Task<IImageConnection?> Open()
    {
        ComicConnection? connection = await _comic.OpenComic();
        if (connection is null)
        {
            return null;
        }

        return new ImageConnection(connection);
    }

    private sealed partial class ImageConnection(ComicConnection connection) : IImageConnection
    {
        public string Fingerprint => connection.GetImageSignature(ComicHandle.COVER_INDEX);

        public void Dispose()
        {
            connection.Dispose();
        }

        public Task<Stream?> OpenImageStream()
        {
            return connection.OpenImageStream(ComicHandle.COVER_INDEX);
        }

        public IVectorImageService? OpenVectorService()
        {
            return connection.OpenVectorService(ComicHandle.COVER_INDEX);
        }
    }
}
