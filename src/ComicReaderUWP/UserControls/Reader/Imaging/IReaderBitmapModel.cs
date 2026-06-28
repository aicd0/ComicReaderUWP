// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Drawing;

namespace ComicReaderUWP.UserControls.Reader.Imaging;

internal interface IReaderBitmapModel : IDisposable
{
    Size SizeInPixels { get; }

    int FrameCount { get; }

    long Duration { get; }

    int GetFrameIndexAtTime(long elapsedMs);

    long GetFrameStartTime(int frameIndex);
}
