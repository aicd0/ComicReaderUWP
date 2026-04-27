// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.UserControls.Reader.PageLayout;

internal class PageLayoutInfo
{
    public int FrameIndex { get; init; }
    public bool IsLastFrame { get; init; }
    public PageLayoutType LayoutType { get; init; }
    public int NeighbourPage { get; init; }
}
