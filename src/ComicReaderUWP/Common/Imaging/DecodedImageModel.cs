// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ComicReaderUWP.Common.Imaging;

internal class DecodedImageModel
{
    public required Image<Bgra32> Image { get; init; }
}
