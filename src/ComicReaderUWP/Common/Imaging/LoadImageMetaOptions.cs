// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Common.Imaging;

internal class LoadImageMetaOptions
{
    public ImageLoaderSchedulerGroup? SchedulerGroup { get; set; }
    public int Priority { get; set; } = 0;

    public LoadImageMetaOptions Clone()
    {
        return new()
        {
            SchedulerGroup = SchedulerGroup,
            Priority = Priority,
        };
    }
}
