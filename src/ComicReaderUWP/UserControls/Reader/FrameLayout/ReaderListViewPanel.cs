// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Windows.Foundation;

namespace ComicReaderUWP.UserControls.Reader.FrameLayout;

internal sealed partial class ReaderListViewPanel : Panel
{
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(Orientation), typeof(ReaderListViewPanel), new PropertyMetadata(Orientation.Vertical, OnOrientationChanged));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public Func<int, Rect?> RequestItemRect = index => null;
    public Func<Size> RequestSize = () => new Size(0, 0);

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
        for (int i = 0; i < Children.Count; i++)
        {
            UIElement child = Children[i];
            Rect? rectNullable = RequestItemRect(i);

            if (rectNullable.HasValue)
            {
                Rect rect = rectNullable.Value;
                double w = double.IsInfinity(rect.Width) || rect.Width < 0 ? 0 : rect.Width;
                double h = double.IsInfinity(rect.Height) || rect.Height < 0 ? 0 : rect.Height;
                child.Measure(new Size(w, h));
            }
            else
            {
                child.Measure(new(100, 100));
            }
        }

        return RequestSize();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (int i = 0; i < Children.Count; i++)
        {
            UIElement child = Children[i];
            Rect? rectNullable = RequestItemRect(i);

            if (rectNullable.HasValue)
            {
                Rect rect = rectNullable.Value;
                child.Arrange(rect);
            }
            else
            {
                child.Arrange(new Rect(0, 0, Math.Max(0, finalSize.Width), Math.Max(0, finalSize.Height)));
            }
        }

        return finalSize;
    }
}
