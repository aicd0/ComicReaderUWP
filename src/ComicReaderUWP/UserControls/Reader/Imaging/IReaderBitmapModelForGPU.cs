// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Graphics.Canvas;

namespace ComicReaderUWP.UserControls.Reader.Imaging;

internal interface IReaderBitmapModelForGPU : IReaderBitmapModel
{
    public CanvasBitmap GetFrameBitmap(int frameIndex);
}
