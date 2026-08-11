// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReaderUWP.Core.Common.DebugTools;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Windows.Foundation;

namespace ComicReaderUWP.UserControls.Reader.FrameLayout;

internal sealed partial class ReaderListViewPanel : Panel
{
    private const string TAG = nameof(ReaderListViewPanel);

    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(Orientation), typeof(ReaderListViewPanel), new PropertyMetadata(Orientation.Vertical, OnOrientationChanged));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

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

    public bool TryGetItemRect(int index, out Rect rect)
    {
        if (index < 0 || index >= Items.Count)
        {
            rect = default;
            return false;
        }

        EnsureCache();
        ItemLayoutCache lastItem = _itemLayoutCache[^1];
        ItemLayoutCache cache = _itemLayoutCache[index];
        rect = Orientation == Orientation.Vertical ?
            new Rect((lastItem.MaxWidthUntilNow - cache.Width) * 0.5, cache.TotalHeightUntilNow - cache.Height, cache.Width, cache.Height) :
            new Rect(cache.TotalWidthUntilNow - cache.Width, (lastItem.MaxHeightUntilNow - cache.Height) * 0.5, cache.Width, cache.Height);
        return true;
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

    private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ReaderListViewPanel panel)
        {
            panel.InvalidateMeasure();
            panel.InvalidateArrange();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double left = double.PositiveInfinity;
        double top = double.PositiveInfinity;
        double right = double.NegativeInfinity;
        double bottom = double.NegativeInfinity;

        for (int i = 0; i < Children.Count; i++)
        {
            UIElement child = Children[i];

            if (TryGetItemRect(i, out Rect rect))
            {
                double w = double.IsInfinity(rect.Width) || rect.Width < 0 ? 0 : rect.Width;
                double h = double.IsInfinity(rect.Height) || rect.Height < 0 ? 0 : rect.Height;
                child.Measure(new Size(w, h));
                left = Math.Min(left, rect.X);
                top = Math.Min(top, rect.Y);
                right = Math.Max(right, rect.Right);
                bottom = Math.Max(bottom, rect.Bottom);
            }
            else
            {
                child.Measure(availableSize);
                Size d = child.DesiredSize;
                left = Math.Min(left, 0);
                top = Math.Min(top, 0);
                right = Math.Max(right, d.Width);
                bottom = Math.Max(bottom, d.Height);
            }
        }

        if (double.IsPositiveInfinity(left))
        {
            left = 0;
        }

        if (double.IsPositiveInfinity(top))
        {
            top = 0;
        }

        if (double.IsNegativeInfinity(right))
        {
            right = 0;
        }

        if (double.IsNegativeInfinity(bottom))
        {
            bottom = 0;
        }

        double finalWidth = Math.Max(0, right - left);
        double finalHeight = Math.Max(0, bottom - top);

        return new Size(finalWidth, finalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (int i = 0; i < Children.Count; i++)
        {
            UIElement child = Children[i];

            if (TryGetItemRect(i, out Rect rect))
            {
                child.Arrange(rect);
            }
            else
            {
                child.Arrange(new Rect(0, 0, Math.Max(0, finalSize.Width), Math.Max(0, finalSize.Height)));
            }
        }

        return finalSize;
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
