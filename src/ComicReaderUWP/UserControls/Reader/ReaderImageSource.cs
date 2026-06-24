// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.UserControls.Reader.Imaging;

namespace ComicReaderUWP.UserControls.Reader;

internal class ReaderImageSource
{
    public required IImageSource Source { get; init; }
    public required ReaderImageSettings Settings { get; init; }
}
