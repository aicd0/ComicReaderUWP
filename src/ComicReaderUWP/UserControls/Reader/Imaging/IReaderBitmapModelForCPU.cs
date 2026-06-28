// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Graphics.Canvas;

namespace ComicReaderUWP.UserControls.Reader.Imaging;

internal interface IReaderBitmapModelForCPU : IReaderBitmapModel
{
    IReaderBitmapModelForGPU UploadToGPU(ICanvasResourceCreator device);
}
