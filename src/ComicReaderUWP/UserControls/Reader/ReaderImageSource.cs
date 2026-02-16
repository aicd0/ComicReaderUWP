// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Data.Models.Misc;

namespace ComicReaderUWP.UserControls.Reader;

internal class ReaderImageSource
{
    public required IImageSource Source { get; init; }
    public required ImageRotationEnum Rotation { get; init; }
    public required bool Flip { get; init; }
}
