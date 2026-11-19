// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Imaging;

public interface IImageConnection : IDisposable
{
    string Path { get; }

    string CacheKey { get; }

    string Fingerprint { get; }

    Task<Stream?> OpenImageStream();

    IVectorImageService? OpenVectorService();
}
