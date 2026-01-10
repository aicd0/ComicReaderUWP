// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ComicReaderUWP.Common.Imaging;

internal partial class DecodedImageModel : IDisposable
{
    public required Image<Bgra32> Image { get; init; }

    public void Dispose()
    {
        Image.Dispose();
    }
}
