// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.Utils;

namespace ComicReaderUWP.Core.Imaging;

public class LoadImageMetaOptions
{
    public ImageLoaderSchedulerGroup? SchedulerGroup { get; set; }
    public int Priority { get; set; } = 0;
    public CancellationSession.IToken? Token { get; set; }

    public LoadImageMetaOptions Clone()
    {
        return new()
        {
            SchedulerGroup = SchedulerGroup,
            Priority = Priority,
            Token = Token,
        };
    }
}
