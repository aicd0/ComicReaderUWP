// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;

namespace ComicReaderUWP.UserControls.Reader.PageLayout;

internal class SimplePageLayoutManager : IPageLayoutManager
{
    public bool TwoPageMode { get; init; } = false;
    public bool EnableCover { get; init; } = true;
    public bool RightToLeft { get; init; } = false;
    public bool SpreadDetection { get; init; } = true;

    private PageInfo?[] _pages = [];
    private int _readyPageCount = 0;

    private int PageCount => _pages.Length;

    public bool Equals(IPageLayoutManager? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is not SimplePageLayoutManager obj)
        {
            return false;
        }

        return TwoPageMode == obj.TwoPageMode
            && EnableCover == obj.EnableCover
            && RightToLeft == obj.RightToLeft
            && SpreadDetection == obj.SpreadDetection;
    }

    public void Reset(int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageCount, nameof(pageCount));
        _pages = new PageInfo?[pageCount];
        _readyPageCount = 0;
    }

    public void AddPage(int page, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page, nameof(page));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(page, PageCount, nameof(page));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width, nameof(width));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height, nameof(height));

        if (_pages[page - 1] is not null)
        {
            throw new InvalidOperationException($"Page {page} has already been added.");
        }

        _pages[page - 1] = new PageInfo { Width = width, Height = height };
        IncreaseReadyIndex();
    }

    public bool TryGetPageLayout(int page, [NotNullWhen(true)] out PageLayoutInfo? layout)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page, nameof(page));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(page, PageCount, nameof(page));

        if (page > _readyPageCount)
        {
            layout = null;
            return false;
        }

        layout = _pages[page - 1]!.Layout!;
        return true;
    }

    private void IncreaseReadyIndex()
    {
        for (int page = _readyPageCount + 1; page <= PageCount; page++)
        {
            PageInfo? pageInfo = _pages[page - 1];
            if (pageInfo is null)
            {
                break;
            }

            int frameIndex;
            bool leftSide;
            int neighbor;
            do
            {
                if (!TwoPageMode)
                {
                    frameIndex = page - 1;
                    leftSide = true;
                    neighbor = ReaderFrameViewModel.NO_PAGE;
                    break;
                }

                if (page >= 2)
                {
                    PageLayoutInfo previousPageLayout = _pages[page - 2]!.Layout!;
                    if (previousPageLayout.NeighbourPage == page)
                    {
                        frameIndex = previousPageLayout.FrameIndex;
                        leftSide = !previousPageLayout.IsLeftSide;
                        neighbor = page - 1;
                        break;
                    }
                }
                else if (EnableCover)
                {
                    frameIndex = 0;
                    leftSide = true;
                    neighbor = ReaderFrameViewModel.NO_PAGE;
                    break;
                }

                if (page >= 2)
                {
                    PageLayoutInfo previousPageLayout = _pages[page - 2]!.Layout!;
                    frameIndex = previousPageLayout.FrameIndex + 1;
                }
                else
                {
                    frameIndex = 0;
                }

                leftSide = !RightToLeft || page == PageCount;
                neighbor = page == PageCount ? ReaderFrameViewModel.NO_PAGE : page + 1;
            } while (false);

            PageLayoutInfo layout = new()
            {
                FrameIndex = frameIndex,
                IsLeftSide = leftSide,
                NeighbourPage = neighbor,
                IsLastFrame = page == PageCount || neighbor == PageCount,
            };
            pageInfo.Layout = layout;
            _readyPageCount = page;
        }
    }

    private class PageInfo
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public PageLayoutInfo? Layout { get; set; }
    }
}
