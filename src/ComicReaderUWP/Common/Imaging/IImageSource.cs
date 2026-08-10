// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Threading.Tasks;

namespace ComicReaderUWP.Common.Imaging;

internal interface IImageSource
{
    string Uri { get; }

    bool ValidateFingerprint { get; }

    Task<string> GetFingerprint();

    Task<Stream?> OpenImageStream();

    Task<IVectorImageService?> OpenVectorService();
}
