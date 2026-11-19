// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Imaging;

public sealed partial class ImageContent(Stream stream, ImageMeta? meta) : IDisposable
{
    public Stream Stream { get; } = stream;

    public ImageMeta? Meta { get; } = meta;

    public void Dispose()
    {
        Stream.Dispose();
    }
}
