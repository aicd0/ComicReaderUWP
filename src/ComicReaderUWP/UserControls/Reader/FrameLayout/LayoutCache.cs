// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReaderUWP.Common.Models.F8;
using ComicReaderUWP.Core.Common.DebugTools;

using Microsoft.UI.Xaml;
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
            new(
                (lastItem.MaxWidthUntilNow - cache.Width) * 0.5,
                cache.TotalHeightUntilNow - cache.Height - cache.Margin.Bottom,
                cache.Width, cache.Height) :
            new(
                cache.TotalWidthUntilNow - cache.Width - cache.Margin.Right,
                (lastItem.MaxHeightUntilNow - cache.Height) * 0.5,
                cache.Width, cache.Height);
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
            double frameWidth = Items[i].Width;
            if (!ValidateLength(frameWidth))
            {
                Logger.F(TAG, $"Invalid frame width {frameWidth}");
                frameWidth = 0.0;
            }

            double frameHeight = Items[i].Height;
            if (!ValidateLength(frameHeight))
            {
                Logger.F(TAG, $"Invalid frame height {frameHeight}");
                frameHeight = 0.0;
            }

            Thickness frameMargin = Items[i].Margin;
            if (!ValidateOffset(frameMargin.Left) ||
                !ValidateOffset(frameMargin.Top) ||
                !ValidateOffset(frameMargin.Right) ||
                !ValidateOffset(frameMargin.Bottom))
            {
                Logger.F(TAG, $"Invalid frame margin {frameMargin}");
                frameMargin = new(0.0);
            }

            double itemWidth = frameWidth + frameMargin.Left + frameMargin.Right;
            double itemHeight = frameHeight + frameMargin.Top + frameMargin.Bottom;
            maxWidth = Math.Max(maxWidth, itemWidth);
            maxHeight = Math.Max(maxHeight, itemHeight);
            totalWidth += itemWidth;
            totalHeight += itemHeight;
            ItemLayoutCache cache = new()
            {
                Width = frameWidth,
                Height = frameHeight,
                Margin = frameMargin,
                MaxWidthUntilNow = maxWidth,
                MaxHeightUntilNow = maxHeight,
                TotalWidthUntilNow = totalWidth,
                TotalHeightUntilNow = totalHeight,
            };
            _itemLayoutCache.Add(cache);
        }
    }

    private static bool ValidateLength(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0.0;
    }

    private static bool ValidateOffset(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private readonly struct ItemLayoutCache
    {
        public required double Height { get; init; }
        public required double Width { get; init; }
        public required Thickness Margin { get; init; }
        public required double MaxHeightUntilNow { get; init; }
        public required double MaxWidthUntilNow { get; init; }
        public required double TotalWidthUntilNow { get; init; }
        public required double TotalHeightUntilNow { get; init; }
    }
}
