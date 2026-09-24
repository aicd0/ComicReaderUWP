// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Threading.Tasks;

using ComicReaderUWP.Common.ErrorHandling;
using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Common.Storage;
using ComicReaderUWP.Data.Models.Comic;

namespace ComicReaderUWP.Helpers.Imaging;

internal sealed partial class ComicImageSource : IImageSource
{
    private readonly ImageLoaderSchedulerGroup _preferredSchedulerGroup;
    private readonly ComicModel _comic;
    private readonly int _index;
    private readonly ComicConnection? _connection;
    private readonly string _uri;

    public string Uri => _uri;

    public ImageLoaderSchedulerGroup PreferredSchedulerGroup => _preferredSchedulerGroup;

    public bool IsCacheValidationEnabled => _connection is not null;

    public ComicImageSource(ComicModel comic, int index, string? uri = null)
    {
        _preferredSchedulerGroup = ImageLoaderSchedulerGroup.FromPath(comic.Location);
        _comic = comic;
        _index = index;
        _connection = null;
        _uri = uri ?? ResourceUri.CreateComicImage(comic.Id, index).ToString();
    }

    public ComicImageSource(ComicModel comic, int index, ComicConnection connection)
    {
        _preferredSchedulerGroup = ImageLoaderSchedulerGroup.FromPath(comic.Location);
        _comic = comic;
        _index = index;
        _connection = connection;
        _uri = connection.GetImageCacheKey(_index);
    }

    public async Task<IImageConnection?> Open()
    {
        if (_connection is null)
        {
            ErrorResult<ComicConnection> connectionErr = await _comic.OpenComic();
            if (!connectionErr.IsSuccessful)
            {
                return null;
            }

            ComicConnection connection = connectionErr.Result;
            if (_index < 0 || _index >= connection.ImageCount)
            {
                connection.Dispose();
                return null;
            }

            return new Connection(connection, _index, ownConnection: true);
        }
        else
        {
            return new Connection(_connection, _index, ownConnection: false);
        }
    }

    private sealed partial class Connection(ComicConnection connection, int index, bool ownConnection) : IImageConnection
    {
        public string Path => connection.GetImagePath(index);

        public string CacheKey => connection.GetImageCacheKey(index);

        public string Fingerprint => connection.GetImageSignature(index);

        public void Dispose()
        {
            if (ownConnection)
            {
                connection.Dispose();
            }
        }

        public Task<Stream?> OpenImageStream()
        {
            return connection.OpenImageStream(index);
        }

        public IVectorImageService? OpenVectorService()
        {
            return connection.OpenVectorService(index);
        }
    }
}
