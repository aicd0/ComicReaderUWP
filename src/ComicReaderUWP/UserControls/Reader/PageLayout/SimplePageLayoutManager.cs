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
    private int _nextLayoutIndex = 0;

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
        _nextLayoutIndex = 0;
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

        if (page > _nextLayoutIndex)
        {
            layout = null;
            return false;
        }

        layout = _pages[page - 1]!.Layout!;
        return true;
    }

    private void IncreaseReadyIndex()
    {
        while (_nextLayoutIndex < PageCount)
        {
            PageInfo? pageInfo = _pages[_nextLayoutIndex];
            if (pageInfo is null)
            {
                break;
            }

            int page = _nextLayoutIndex + 1;
            int frameIndex;
            bool leftSide;
            int neighbor;

            if (TwoPageMode)
            {
                if (EnableCover)
                {
                    if (RightToLeft)
                    {
                        frameIndex = page / 2;
                        leftSide = page == PageCount || page % 2 == 1;
                        neighbor = (page > 1 && (PageCount % 2 == 1 || page < PageCount)) ? (leftSide ? page - 1 : page + 1) : ReaderFrameViewModel.NO_PAGE;
                    }
                    else
                    {
                        frameIndex = page / 2;
                        leftSide = page == 1 || page % 2 == 0;
                        neighbor = (page > 1 && (PageCount % 2 == 1 || page < PageCount)) ? (leftSide ? page + 1 : page - 1) : ReaderFrameViewModel.NO_PAGE;
                    }
                }
                else
                {
                    if (RightToLeft)
                    {
                        frameIndex = (page - 1) / 2;
                        leftSide = page == PageCount || page % 2 == 0;
                        neighbor = (PageCount % 2 == 0 || page < PageCount) ? (leftSide ? page - 1 : page + 1) : ReaderFrameViewModel.NO_PAGE;
                    }
                    else
                    {
                        frameIndex = (page - 1) / 2;
                        leftSide = page % 2 == 1;
                        neighbor = (PageCount % 2 == 0 || page < PageCount) ? (leftSide ? page + 1 : page - 1) : ReaderFrameViewModel.NO_PAGE;
                    }
                }
            }
            else
            {
                frameIndex = page - 1;
                leftSide = true;
                neighbor = ReaderFrameViewModel.NO_PAGE;
            }

            PageLayoutInfo layout = new()
            {
                FrameIndex = frameIndex,
                IsLeftSide = leftSide,
                NeighbourPage = neighbor,
                IsLastFrame = page == PageCount || neighbor == PageCount,
            };
            pageInfo.Layout = layout;

            _nextLayoutIndex++;
        }
    }

    private class PageInfo
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public PageLayoutInfo? Layout { get; set; }
    }
}
