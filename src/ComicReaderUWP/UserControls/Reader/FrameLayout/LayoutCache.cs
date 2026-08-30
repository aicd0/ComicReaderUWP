// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReaderUWP.Common.Models.F8;
using ComicReaderUWP.Core.Common.DebugTools;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.UserControls.Reader.FrameLayout;

internal class LayoutCache
{
    private const string TAG = nameof(LayoutCache);

    public IReadOnlyList<IReaderListViewItemViewModel> Items { get; set; } = [];

    private readonly List<ItemLayoutCache> _itemLayoutCache = [];

    public void InvalidateCache(int startIndex = 0)
    {
        if (startIndex >= _itemLayoutCache.Count)
        {
            return;
        }

        _itemLayoutCache.RemoveRange(startIndex, _itemLayoutCache.Count - startIndex);
    }

    public bool TryGetItemRect(int index, out RectF8 rect, Orientation orientation)
    {
        if (index < 0 || index >= Items.Count)
        {
            rect = default;
            return false;
        }

        EnsureCache();
        ItemLayoutCache lastItem = _itemLayoutCache[^1];
        ItemLayoutCache cache = _itemLayoutCache[index];
        rect = orientation == Orientation.Vertical ?
            new((lastItem.MaxWidthUntilNow - cache.Width) * 0.5, cache.TotalHeightUntilNow - cache.Height, cache.Width, cache.Height) :
            new(cache.TotalWidthUntilNow - cache.Width, (lastItem.MaxHeightUntilNow - cache.Height) * 0.5, cache.Width, cache.Height);
        return true;
    }

    public SizeF8 GetSize(Orientation orientation)
    {
        if (Items.Count == 0)
        {
            return new(0, 0);
        }

        EnsureCache();
        ItemLayoutCache lastItem = _itemLayoutCache[^1];
        return orientation == Orientation.Vertical ?
            new(lastItem.MaxWidthUntilNow, lastItem.TotalHeightUntilNow) :
            new(lastItem.TotalWidthUntilNow, lastItem.MaxHeightUntilNow);
    }

    private void EnsureCache()
    {
        if (_itemLayoutCache.Count >= Items.Count)
        {
            return;
        }

        double maxWidth = 0, maxHeight = 0;
        double totalWidth = 0, totalHeight = 0;
        if (_itemLayoutCache.Count > 0)
        {
            ItemLayoutCache last = _itemLayoutCache[^1];
            maxWidth = last.MaxWidthUntilNow;
            maxHeight = last.MaxHeightUntilNow;
            totalWidth = last.TotalWidthUntilNow;
            totalHeight = last.TotalHeightUntilNow;
        }

        for (int i = _itemLayoutCache.Count; i < Items.Count; i++)
        {
            double itemWidth = Items[i].Width;
            if (double.IsNaN(itemWidth) || double.IsInfinity(itemWidth) || itemWidth < 0.0)
            {
                itemWidth = 0.0;
                Logger.F(TAG, $"Invalid item width {itemWidth}");
            }

            double itemHeight = Items[i].Height;
            if (double.IsNaN(itemHeight) || double.IsInfinity(itemHeight) || itemHeight < 0.0)
            {
                itemHeight = 0.0;
                Logger.F(TAG, $"Invalid item height {itemHeight}");
            }

            maxWidth = Math.Max(maxWidth, itemWidth);
            maxHeight = Math.Max(maxHeight, itemHeight);
            totalWidth += itemWidth;
            totalHeight += itemHeight;
            ItemLayoutCache cache = new()
            {
                Width = itemWidth,
                Height = itemHeight,
                MaxWidthUntilNow = maxWidth,
                MaxHeightUntilNow = maxHeight,
                TotalWidthUntilNow = totalWidth,
                TotalHeightUntilNow = totalHeight,
            };
            _itemLayoutCache.Add(cache);
        }
    }

    private readonly struct ItemLayoutCache
    {
        public required double Height { get; init; }
        public required double Width { get; init; }
        public required double MaxHeightUntilNow { get; init; }
        public required double MaxWidthUntilNow { get; init; }
        public required double TotalWidthUntilNow { get; init; }
        public required double TotalHeightUntilNow { get; init; }
    }
}
