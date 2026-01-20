// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;

using ComicReaderUWP.Common.Imaging;

namespace ComicReaderUWP.Data.Models.Comic;

internal interface IComicConnection : IDisposable
{
    int GetImageCount();

    string GetImageName(int index);

    string GetImageCacheKey(int index);

    string GetImageSignature(int index);

    Stream? OpenImageStream(int index);

    IVectorImageService? OpenVectorService(int index);
}
