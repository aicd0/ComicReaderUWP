// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Data.Models.Comic;

namespace ComicReaderUWP.Helpers.Imaging;

internal sealed partial class ComicImageSource(ComicModel comic, ComicConnection connection, int index) : IImageSource
{
    private readonly ImageLoaderSchedulerGroup _preferredSchedulerGroup = ImageLoaderSchedulerGroup.FromPath(comic.Location);
    private readonly ComicConnection _connection = connection;
    private readonly int _index = index;

    public string Uri => _connection.GetImageCacheKey(_index);

    public ImageLoaderSchedulerGroup PreferredSchedulerGroup => _preferredSchedulerGroup;

    public bool ValidateFingerprint => true;

    public async Task<IImageConnection?> Open()
    {
        return new ImageConnection(_connection, _index);
    }

    private sealed partial class ImageConnection(ComicConnection connection, int index) : IImageConnection
    {
        public string Fingerprint => connection.GetImageSignature(index);

        public void Dispose()
        {
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
