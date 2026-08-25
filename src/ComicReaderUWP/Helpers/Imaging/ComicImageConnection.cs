// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Data.Models.Comic;

namespace ComicReaderUWP.Helpers.Imaging;

internal sealed partial class ComicImageConnection(ComicConnection connection, int index, bool ownConnection) : IImageConnection
{
    public string Path => connection.GetImagePath(index);

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
