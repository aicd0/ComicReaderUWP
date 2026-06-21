// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;

using ComicReaderUWP.Core.Test;
using ComicReaderUWP.UserControls.Reader.Imaging;

namespace ComicReaderUWP.UserControls.Reader.PageLayout;

internal class SimplePageLayoutManager : IPageLayoutManager
{
    public bool TwoPageMode { get; init; } = false;
    public int CoverPageCount { get; init; } = 1;
    public bool RightToLeft { get; init; } = false;
    public bool SpreadDetection { get; init; } = false;

    private PageInfo?[] _pages = [];
    private readonly SpreadDetectionHelper.PageSamples _samples = new();
    private int _readyCoverPageCount = 0;

    private int PageCount => _pages.Length;
    private int AddedPageCount { get; set; } = 0;
    private int ReadyPageCount { get; set; } = 0;

    public bool EquivalentTo(IPageLayoutManager other)
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
            && CoverPageCount == obj.CoverPageCount
            && RightToLeft == obj.RightToLeft
            && SpreadDetection == obj.SpreadDetection;
    }

    public void Reset(int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageCount, nameof(pageCount));
        _pages = new PageInfo?[pageCount];
        _samples.Clear();
        _readyCoverPageCount = 0;
        AddedPageCount = 0;
        ReadyPageCount = 0;
    }

    public void AddPage(int page, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page, nameof(page));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(page, PageCount, nameof(page));
        ArgumentOutOfRangeException.ThrowIfNegative(width, nameof(width));
        ArgumentOutOfRangeException.ThrowIfNegative(height, nameof(height));

        if (_pages[page - 1] is not null)
        {
            throw new InvalidOperationException($"Page {page} has already been added.");
        }

        _pages[page - 1] = new PageInfo { Width = width, Height = height };
        AddedPageCount++;

        if (Math.Min(width, height) > 0)
        {
            float aspectRatio = (float)width / height;
            _samples.Add(aspectRatio);
        }

        IncreaseReadyIndex();
    }

    public bool TryGetPageLayout(int page, [NotNullWhen(true)] out PageLayoutInfo? layout)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page, nameof(page));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(page, PageCount, nameof(page));

        if (page > ReadyPageCount)
        {
            layout = null;
            return false;
        }

        layout = _pages[page - 1]!.Layout!;
        return true;
    }

    private void IncreaseReadyIndex()
    {
        for (int page = ReadyPageCount + 1; page <= PageCount; page++)
        {
            PageInfo? pageInfo = _pages[page - 1];
            if (pageInfo is null)
            {
                break;
            }

            if (!TryCreateLayout(page, out PageLayoutInfo? layout))
            {
                break;
            }

            pageInfo.Layout = layout;
            ReadyPageCount = page;
        }
    }

    private bool TryCreateLayout(int page, [NotNullWhen(true)] out PageLayoutInfo? layout)
    {
        PageLayoutInfo? previousPageLayout = page >= 2 ? _pages[page - 2]!.Layout! : null;
        if (previousPageLayout?.NeighbourPage == page)
        {
            PageLayoutType layoutType = previousPageLayout.LayoutType == PageLayoutType.Left ? PageLayoutType.Right : PageLayoutType.Left;
            layout = CreateLayout(page, previousPageLayout.FrameIndex, layoutType, page - 1);
            return true;
        }

        int frameIndex = previousPageLayout is null ? 0 : previousPageLayout.FrameIndex + 1;
        bool isLastPage = page == PageCount;
        bool requireCompletion = AddedPageCount == PageCount;

        if (SpreadDetection)
        {
            if (_samples.Count <= 6 && !requireCompletion)
            {
                // Requires at least 6 samples to be reliable
                layout = null;
                return false;
            }

            if (!TryCheckSpreadPage(page, out bool isSpreadPage))
            {
                throw new InvalidOperationException("Failed to check if current page is a spread page.");
            }

            if (isSpreadPage)
            {
                layout = CreateLayout(page, frameIndex, PageLayoutType.Spread, ReaderFrameViewModel.NO_PAGE);
                return true;
            }

            if (!isLastPage)
            {
                if (TryCheckSpreadPage(page + 1, out isSpreadPage))
                {
                    if (isSpreadPage)
                    {
                        layout = CreateLayout(page, frameIndex, PageLayoutType.Single, ReaderFrameViewModel.NO_PAGE);
                        return true;
                    }
                }
                else if (!requireCompletion)
                {
                    // Next page is not ready, cannot determine the layout of the current page
                    layout = null;
                    return false;
                }
            }
        }

        if (!TwoPageMode)
        {
            layout = CreateLayout(page, frameIndex, PageLayoutType.Single, ReaderFrameViewModel.NO_PAGE);
            return true;
        }

        if (_readyCoverPageCount < CoverPageCount)
        {
            _readyCoverPageCount++;
            layout = CreateLayout(page, frameIndex, PageLayoutType.Single, ReaderFrameViewModel.NO_PAGE);
            return true;
        }

        {
            PageLayoutType layoutType = isLastPage ? PageLayoutType.Single : (RightToLeft ? PageLayoutType.Right : PageLayoutType.Left);
            int neighbor = isLastPage ? ReaderFrameViewModel.NO_PAGE : page + 1;
            layout = CreateLayout(page, frameIndex, layoutType, neighbor);
            return true;
        }
    }

    private PageLayoutInfo CreateLayout(int page, int frameIndex, PageLayoutType layoutType, int neighbor)
    {
        return new PageLayoutInfo
        {
            FrameIndex = frameIndex,
            IsLastFrame = page == PageCount || neighbor == PageCount,
            LayoutType = layoutType,
            NeighbourPage = neighbor,
        };
    }

    private bool TryCheckSpreadPage(int page, out bool isSpreadPage)
    {
        isSpreadPage = false;
        if (page < 1 || page > PageCount)
        {
            return false;
        }

        PageInfo? pageInfo = _pages[page - 1];
        if (pageInfo is null)
        {
            return false;
        }

        if (Math.Min(pageInfo.Width, pageInfo.Height) <= 0)
        {
            isSpreadPage = false;
            return true;
        }

        float aspectRatio = (float)pageInfo.Width / pageInfo.Height;
        isSpreadPage = _samples.IsSpreadPage(aspectRatio);
        return true;
    }

    private class PageInfo
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public PageLayoutInfo? Layout { get; set; }
    }
}
