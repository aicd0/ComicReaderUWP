// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;

using ComicReaderUWP.Core.Imaging;

namespace ComicReaderUWP.Helpers.Imaging;

internal class EmptyImageSource : IImageSource
{
    public static EmptyImageSource Instance { get; } = new();

    public string Uri => string.Empty;

    public ImageLoaderSchedulerGroup PreferredSchedulerGroup => ImageLoaderSchedulerGroup.Default;

    public ImageCacheValidationMode CacheValidationMode => ImageCacheValidationMode.None;

    private EmptyImageSource() { }

    public Task<IImageConnection?> Open()
    {
        return Task.FromResult<IImageConnection?>(null);
    }
}
