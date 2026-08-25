// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;

namespace ComicReaderUWP.Common.Imaging;

internal interface IImageConnection : IDisposable
{
    string Path { get; }

    string Fingerprint { get; }

    Task<Stream?> OpenImageStream();

    IVectorImageService? OpenVectorService();
}
