// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;

using ComicReaderUWP.Common.Utils;

using Microsoft.UI.Composition;

namespace ComicReaderUWP.UserControls.Reader.Imaging;

internal class CompositionGroupModel
{
    private static int _idCounter = 0;

    public int Id { get; } = Interlocked.Increment(ref _idCounter);
    public required RefCounted<CompositionDrawingSurface> SurfaceRef { get; init; }
    public required IReadOnlyList<CompositionItemModel> Items { get; init; }
}
