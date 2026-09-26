// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Imaging;

namespace ComicReaderUWP.Common.Storage;

internal interface IResourceUriHandler
{
    Uri Uri { get; }

    Task<IImageSource?> ResolveImage();

    Task Release();
}
