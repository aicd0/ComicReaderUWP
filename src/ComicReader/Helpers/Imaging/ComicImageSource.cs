// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;

using ComicReader.Common.Imaging;
using ComicReader.Data.Models.Comic;

using Windows.Storage.Streams;

namespace ComicReader.Helpers.Imaging;

internal class ComicImageSource(ComicModel comic, IComicConnection connection, int index) : IImageSource
{
    private readonly ComicModel _comic = comic;
    private readonly IComicConnection _connection = connection;
    private readonly int _index = index;

    public async Task<IRandomAccessStream?> GetImageStream()
    {
        return await _connection.GetImageStream(_index);
    }

    public string GetUri()
    {
        return _comic.GetImageCacheKey(_index);
    }

    public string GetContentFingerprint()
    {
        int fingerprint = _comic.GetImageSignature(_index);
        if (fingerprint == 0)
        {
            return string.Empty;
        }

        return fingerprint.ToString();
    }
}
