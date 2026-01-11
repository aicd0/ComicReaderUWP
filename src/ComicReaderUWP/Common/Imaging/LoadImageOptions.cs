// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.Utils;

namespace ComicReaderUWP.Common.Imaging;

internal class LoadImageOptions
{
    public required CancellationSession.IToken Token { get; set; }
    public required IImageResultHandler Handler { get; set; }
    public double FrameWidth { get; set; }
    public double FrameHeight { get; set; }
    public StretchModeEnum StretchMode { get; set; } = StretchModeEnum.Uniform;

    public LoadImageOptions Clone()
    {
        return new()
        {
            Token = Token,
            Handler = Handler,
            FrameWidth = FrameWidth,
            FrameHeight = FrameHeight,
            StretchMode = StretchMode,
        };
    }
}
