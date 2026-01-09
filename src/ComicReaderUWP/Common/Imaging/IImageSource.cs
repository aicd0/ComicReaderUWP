// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;

using Windows.Storage.Streams;

namespace ComicReaderUWP.Common.Imaging;

internal interface IImageSource
{
    Task<IRandomAccessStream?> GetImageStream();

    string GetUri();

    string GetContentFingerprint();
}
