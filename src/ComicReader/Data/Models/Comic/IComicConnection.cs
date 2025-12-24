// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using Windows.Storage.Streams;

namespace ComicReader.Data.Models.Comic;

internal interface IComicConnection : IDisposable
{
    int GetImageCount();

    Task<IRandomAccessStream?> GetImageStream(int index);

    string GetImageName(int index);

    string GetImageCacheKey(int index);

    int GetImageSignature(int index);
}
