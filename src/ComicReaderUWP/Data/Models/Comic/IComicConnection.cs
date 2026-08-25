// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Imaging;

namespace ComicReaderUWP.Data.Models.Comic;

internal interface IComicConnection : IDisposable
{
    int ImageCount { get; }

    string GetImageName(int index);

    string GetImagePath(int index);

    string GetImageCacheKey(int index);

    string GetImageSignature(int index);

    Task<Stream?> OpenImageStream(int index);

    IVectorImageService? OpenVectorService(int index);
}
