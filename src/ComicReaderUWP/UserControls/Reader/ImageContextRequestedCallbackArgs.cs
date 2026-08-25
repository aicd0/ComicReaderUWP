// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.Imaging;

namespace ComicReaderUWP.UserControls.Reader;

internal class ImageContextRequestedCallbackArgs
{
    public required int ImageIndex { get; init; }
    public required IImageSource Image { get; init; }
}
