// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;

namespace ComicReaderUWP.Triggers;

public class SizeTrigger : StateTriggerBase
{
    public FrameworkElement Target
    {
        get => (FrameworkElement)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    public static readonly DependencyProperty TargetProperty =
        DependencyProperty.Register(nameof(Target), typeof(FrameworkElement),
            typeof(SizeTrigger), new PropertyMetadata(null, OnTargetChanged));

    public double MinWidth { get; set; }
    public double MinHeight { get; set; }

    private static void OnTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SizeTrigger trigger && e.NewValue is FrameworkElement fe)
        {
            fe.SizeChanged += (_, __) => trigger.UpdateState(fe);
        }
    }

    private void UpdateState(FrameworkElement fe)
    {
        bool active = fe.ActualWidth >= MinWidth && fe.ActualHeight >= MinHeight;
        SetActive(active);
    }
}
