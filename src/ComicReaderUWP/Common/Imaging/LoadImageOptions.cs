// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.Utils;

namespace ComicReaderUWP.Common.Imaging;

internal class LoadImageOptions
{
    public required CancellationSession.IToken Token { get; set; }
    public required IImageResultHandler Handler { get; set; }
    public double DecodeWidth { get; set; }
    public double DecodeHeight { get; set; }
    public ImageLoaderSchedulerGroup? SchedulerGroup { get; set; }
    public int Priority { get; set; } = 0;

    public LoadImageOptions Clone()
    {
        return new()
        {
            Token = Token,
            Handler = Handler,
            DecodeWidth = DecodeWidth,
            DecodeHeight = DecodeHeight,
            SchedulerGroup = SchedulerGroup,
            Priority = Priority,
        };
    }
}
