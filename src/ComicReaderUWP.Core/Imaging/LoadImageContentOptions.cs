// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Imaging;

public class LoadImageContentOptions
{
    public ImageLoaderSchedulerGroup? SchedulerGroup { get; set; }
    public int Priority { get; set; } = 0;

    public LoadImageContentOptions Clone()
    {
        return new()
        {
            SchedulerGroup = SchedulerGroup,
            Priority = Priority,
        };
    }
}
