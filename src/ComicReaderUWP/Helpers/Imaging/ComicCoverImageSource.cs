// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Data.Models.Comic;

namespace ComicReaderUWP.Helpers.Imaging;

internal sealed partial class ComicCoverImageSource : IImageSource
{
    public static async Task<ComicCoverImageSource> Create(ComicModel comic)
    {
        string? coverCacheKey = comic.GetExt(ComicExt.COVER_CACHE_KEY);
        if (string.IsNullOrEmpty(coverCacheKey))
        {
            using ComicConnection? connection = await comic.OpenComic();
            coverCacheKey = comic.GetExt(ComicExt.COVER_CACHE_KEY) ?? string.Empty;
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

        string? coverIndexString = _comic.GetExt(ComicExt.COVER_INDEX);
        if (string.IsNullOrEmpty(coverIndexString) || !int.TryParse(coverIndexString, out int coverIndex))
        {
            coverIndex = 0;
        }

        return new ComicImageConnection(connection, coverIndex, ownConnection: true);
    }
}
