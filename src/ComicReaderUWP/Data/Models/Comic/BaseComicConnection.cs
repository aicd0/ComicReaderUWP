// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Imaging;

namespace ComicReaderUWP.Data.Models.Comic;

internal abstract class BaseComicConnection : IComicConnection
{
    public abstract int ImageCount { get; }

    public abstract void Dispose();

    public abstract string GetImageCacheKey(int index);

    public virtual string GetImagePath(int index)
    {
        return string.Empty;
    }

    public virtual string GetImageName(int index)
    {
        return string.Empty;
    }

    public abstract string GetImageSignature(int index);

    public abstract Task<Stream?> OpenImageStream(int index);

    public virtual IVectorImageService? OpenVectorService(int index)
    {
        return null;
    }
}
