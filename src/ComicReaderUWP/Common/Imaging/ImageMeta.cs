// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Common.Imaging;

internal class ImageMeta
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required double DpiX { get; init; }
    public required double DpiY { get; init; }
    public required string Format { get; init; }
    public required int BitsPerPixel { get; init; }
    public required long Size { get; init; }
    public required int FrameCount { get; init; }
}
