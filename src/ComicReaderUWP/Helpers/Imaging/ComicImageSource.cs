// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Data.Models.Comic;

namespace ComicReaderUWP.Helpers.Imaging;

internal sealed class ComicImageSource(ComicModel comic, ComicConnection connection, int index) : IImageSource
{
    private readonly ImageLoaderSchedulerGroup _preferredSchedulerGroup = ImageLoaderSchedulerGroup.FromPath(comic.Location);
    private readonly ComicConnection _connection = connection;
    private readonly int _index = index;

    public string Uri => _connection.GetImageCacheKey(_index);

    public ImageLoaderSchedulerGroup PreferredSchedulerGroup => _preferredSchedulerGroup;

    public bool ValidateFingerprint => true;

    public async Task<IImageConnection?> Open()
    {
        return new ComicImageConnection(_connection, _index, ownConnection: false);
    }
}
