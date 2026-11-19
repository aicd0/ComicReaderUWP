// Copyright (c) aicd0. All rights reserved.
// Licensed under the GNU General Public License v3.0 or later.

namespace ComicReaderUWP.Core.Imaging;

public class LoadImageMetaOptions
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
