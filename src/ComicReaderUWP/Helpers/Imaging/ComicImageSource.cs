// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Data.Models.Comic;

using Windows.Storage.Streams;

namespace ComicReaderUWP.Helpers.Imaging;

internal class ComicImageSource(IComicConnection connection, int index) : IImageSource
{
    private readonly IComicConnection _connection = connection;
    private readonly int _index = index;

    public async Task<IRandomAccessStream?> GetImageStream()
    {
        return await _connection.GetImageStream(_index);
    }

    public string GetUri()
    {
        return _connection.GetImageCacheKey(_index);
    }

    public string GetContentFingerprint()
    {
        return _connection.GetImageSignature(_index);
    }
}
