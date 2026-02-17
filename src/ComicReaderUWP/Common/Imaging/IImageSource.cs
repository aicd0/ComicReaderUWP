// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.IO;

namespace ComicReaderUWP.Common.Imaging;

internal interface IImageSource
{
    string Uri { get; }

    bool ValidateFingerprint { get; }

    string CalculateFingerprint();

    Stream? OpenImageStream();

    IVectorImageService? OpenVectorService();
}
