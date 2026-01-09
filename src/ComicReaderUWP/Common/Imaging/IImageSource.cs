// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.IO;

namespace ComicReaderUWP.Common.Imaging;

internal interface IImageSource
{
    Stream? GetImageStream();

    string GetUri();

    string GetContentFingerprint();
}
