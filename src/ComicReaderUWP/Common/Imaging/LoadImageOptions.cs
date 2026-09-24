// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.Utils;

using Microsoft.UI.Xaml.Media;

namespace ComicReaderUWP.Common.Imaging;

internal class LoadImageOptions
{
    public required CancellationSession.IToken Token { get; set; }
    public required IImageResultHandler Handler { get; set; }
    public double FrameWidth { get; set; }
    public double FrameHeight { get; set; }
    public Stretch Stretch { get; set; } = Stretch.Uniform;
    public ImageLoaderSchedulerGroup? SchedulerGroup { get; set; }
    public int Priority { get; set; } = 0;

    public LoadImageOptions Clone()
    {
        return new()
        {
            Token = Token,
            Handler = Handler,
            FrameWidth = FrameWidth,
            FrameHeight = FrameHeight,
            Stretch = Stretch,
            SchedulerGroup = SchedulerGroup,
            Priority = Priority,
        };
    }
}
