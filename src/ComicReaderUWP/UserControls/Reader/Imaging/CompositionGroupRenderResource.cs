// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Microsoft.Graphics.Canvas;
using Microsoft.UI.Composition;

namespace ComicReaderUWP.UserControls.Reader.Imaging;

internal sealed partial class CompositionGroupRenderResource : IDisposable
{
    public required CompositionBrush Brush { get; init; }
    public required CompositionDrawingSurface Surface { get; init; }
    public required CanvasRenderTarget OffscreenCanvas { get; init; }

    public void Dispose()
    {
        Brush.Dispose();
        Surface.Dispose();
        OffscreenCanvas.Dispose();
    }
}
