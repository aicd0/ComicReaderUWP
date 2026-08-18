// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Data.Models.Comic;

namespace ComicReaderUWP.Helpers.Imaging;

internal class ComicImageSource(ComicModel comic, ComicConnection connection, int index) : IImageSource
{
    private readonly ImageLoaderSchedulerGroup _preferredSchedulerGroup = ImageLoaderSchedulerGroup.FromPath(comic.Location);
    private readonly ComicConnection _connection = connection;
    private readonly int _index = index;

    public string Uri => _connection.GetImageCacheKey(_index);

    public ImageLoaderSchedulerGroup PreferredSchedulerGroup => _preferredSchedulerGroup;

    public bool ValidateFingerprint => true;

    public async Task<string> GetFingerprint()
    {
        return _connection.GetImageSignature(_index);
    }

    public Task<Stream?> OpenImageStream()
    {
        return _connection.OpenImageStream(_index);
    }

    public async Task<IVectorImageService?> OpenVectorService()
    {
        return _connection.OpenVectorService(_index);
    }
}
