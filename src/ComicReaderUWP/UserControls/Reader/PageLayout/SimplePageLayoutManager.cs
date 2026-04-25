// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;

using ComicReaderUWP.Data.Models.Misc;

namespace ComicReaderUWP.UserControls.Reader.PageLayout;

internal class SimplePageLayoutManager(PageArrangementEnum arrangement) : IPageLayoutManager
{
    private readonly PageArrangementEnum _arrangement = arrangement;
    private PageInfo?[] _pages = [];

    private int PageCount => _pages.Length;

    public bool Equals(IPageLayoutManager? other)
    {
        return other is SimplePageLayoutManager manager && _arrangement == manager._arrangement;
    }

    public void Reset(int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageCount, nameof(pageCount));
        _pages = new PageInfo?[pageCount];
    }

    public void AddPage(int page, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page, nameof(page));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(page, PageCount, nameof(page));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width, nameof(width));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height, nameof(height));

        _pages[page - 1] = new PageInfo { Width = width, Height = height };
    }

    public bool TryGetPageLayout(int page, [NotNullWhen(true)] out PageLayoutInfo? layout)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page, nameof(page));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(page, PageCount, nameof(page));

        PageInfo? pageInfo = _pages[page - 1];
        if (pageInfo is null)
        {
            layout = null;
            return false;
        }

        if (pageInfo.Layout is not null)
        {
            layout = pageInfo.Layout;
            return true;
        }

        int frameIndex;
        bool leftSide;
        int neighbor;
        bool lastFrame;
        switch (_arrangement)
        {
            case PageArrangementEnum.Single:
                frameIndex = page - 1;
                leftSide = true;
                neighbor = ReaderFrameViewModel.NO_PAGE;
                lastFrame = page == PageCount;
                break;
            case PageArrangementEnum.DualCover:
                frameIndex = page / 2;
                leftSide = page == 1 || page % 2 == 0;
                neighbor = (page > 1 && (PageCount % 2 == 1 || page < PageCount)) ? (leftSide ? page + 1 : page - 1) : ReaderFrameViewModel.NO_PAGE;
                lastFrame = page == PageCount || (page == PageCount - 1 && neighbor != ReaderFrameViewModel.NO_PAGE);
                break;
            case PageArrangementEnum.DualCoverMirror:
                frameIndex = page / 2;
                leftSide = page == PageCount || page % 2 == 1;
                neighbor = (page > 1 && (PageCount % 2 == 1 || page < PageCount)) ? (leftSide ? page - 1 : page + 1) : ReaderFrameViewModel.NO_PAGE;
                lastFrame = page == PageCount || (page == PageCount - 1 && neighbor != ReaderFrameViewModel.NO_PAGE);
                break;
            case PageArrangementEnum.DualNoCover:
                frameIndex = (page - 1) / 2;
                leftSide = page % 2 == 1;
                neighbor = (PageCount % 2 == 0 || page < PageCount) ? (leftSide ? page + 1 : page - 1) : ReaderFrameViewModel.NO_PAGE;
                lastFrame = page == PageCount || (page == PageCount - 1 && neighbor != ReaderFrameViewModel.NO_PAGE);
                break;
            case PageArrangementEnum.DualNoCoverMirror:
                frameIndex = (page - 1) / 2;
                leftSide = page == PageCount || page % 2 == 0;
                neighbor = (PageCount % 2 == 0 || page < PageCount) ? (leftSide ? page - 1 : page + 1) : ReaderFrameViewModel.NO_PAGE;
                lastFrame = page == PageCount || (page == PageCount - 1 && neighbor != ReaderFrameViewModel.NO_PAGE);
                break;
            default:
                throw new InvalidOperationException($"Unsupported page arrangement: {_arrangement}");
        }

        layout = new()
        {
            FrameIndex = frameIndex,
            IsLeftSide = leftSide,
            NeighbourPage = neighbor,
            IsLastFrame = lastFrame,
        };
        pageInfo.Layout = layout;
        return true;
    }

    private class PageInfo
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public PageLayoutInfo? Layout { get; set; }
    }
}
