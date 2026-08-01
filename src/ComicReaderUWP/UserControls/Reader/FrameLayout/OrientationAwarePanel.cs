// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Windows.Foundation;

namespace ComicReaderUWP.UserControls.Reader.FrameLayout;

public sealed partial class OrientationAwarePanel : Panel
{
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(Orientation), typeof(OrientationAwarePanel), new PropertyMetadata(Orientation.Vertical, OnOrientationChanged));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OrientationAwarePanel panel)
        {
            panel.InvalidateMeasure();
            panel.InvalidateArrange();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return Orientation == Orientation.Vertical
            ? MeasureVertical(availableSize)
            : MeasureHorizontal(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        return Orientation == Orientation.Vertical
            ? ArrangeVertical(finalSize)
            : ArrangeHorizontal(finalSize);
    }

    private Size MeasureVertical(Size availableSize)
    {
        double width = 0;
        double height = 0;

        foreach (UIElement child in Children)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            Size desired = child.DesiredSize;
            width = Math.Max(width, desired.Width);
            height += desired.Height;
        }

        return new Size(width, height);
    }

    private Size ArrangeVertical(Size finalSize)
    {
        double y = 0;

        foreach (UIElement child in Children)
        {
            Size desired = child.DesiredSize;
            child.Arrange(new Rect(0, y, finalSize.Width, desired.Height));
            y += desired.Height;
        }

        return new Size(finalSize.Width, y);
    }

    private Size MeasureHorizontal(Size availableSize)
    {
        double width = 0;
        double height = 0;

        foreach (UIElement child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            Size desired = child.DesiredSize;
            width += desired.Width;
            height = Math.Max(height, desired.Height);
        }

        return new Size(width, height);
    }

    private Size ArrangeHorizontal(Size finalSize)
    {
        double x = 0;

        foreach (UIElement child in Children)
        {
            Size desired = child.DesiredSize;
            child.Arrange(new Rect(x, 0, desired.Width, finalSize.Height));
            x += desired.Width;
        }

        return finalSize;
    }
}
