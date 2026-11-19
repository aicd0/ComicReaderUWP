// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Imaging;

public interface IImageSource
{
    string Uri { get; }

    ImageLoaderSchedulerGroup PreferredSchedulerGroup { get; }

    ImageCacheValidationMode CacheValidationMode { get; }

    Task<IImageConnection?> Open();
}
