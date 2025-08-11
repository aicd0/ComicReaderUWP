// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using Windows.Storage.Streams;

namespace ComicReader.Data.Models.Comic;

internal interface IComicConnection : IDisposable
{
    public int GetImageCount();

    public Task<IRandomAccessStream?> GetImageStream(int index);

    public string GetImageName(int index);
}
