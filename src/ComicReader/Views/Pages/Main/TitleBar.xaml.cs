// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace ComicReader.Views.Pages.Main;

public sealed partial class TitleBar : UserControl
{
    public TitleBar()
    {
        InitializeComponent();
    }

    public SolidColorBrush ButtonBackground
    {
        get { return (SolidColorBrush)GetValue(ButtonBackgroundProperty); }
        set { SetValue(ButtonBackgroundProperty, value); }
    }
    public static readonly DependencyProperty ButtonBackgroundProperty =
        DependencyProperty.Register(nameof(ButtonBackground), typeof(SolidColorBrush), typeof(TitleBar), new PropertyMetadata(null));

    public SolidColorBrush ButtonForeground
    {
        get { return (SolidColorBrush)GetValue(ButtonForegroundProperty); }
        set { SetValue(ButtonForegroundProperty, value); }
    }
    public static readonly DependencyProperty ButtonForegroundProperty =
        DependencyProperty.Register(nameof(ButtonForeground), typeof(SolidColorBrush), typeof(TitleBar), new PropertyMetadata(null));

    public SolidColorBrush ButtonInactiveBackground
    {
        get { return (SolidColorBrush)GetValue(ButtonInactiveBackgroundProperty); }
        set { SetValue(ButtonInactiveBackgroundProperty, value); }
    }
    public static readonly DependencyProperty ButtonInactiveBackgroundProperty =
        DependencyProperty.Register(nameof(ButtonInactiveBackground), typeof(SolidColorBrush), typeof(TitleBar), new PropertyMetadata(null));

    public SolidColorBrush ButtonInactiveForeground
    {
        get { return (SolidColorBrush)GetValue(ButtonInactiveForegroundProperty); }
        set { SetValue(ButtonInactiveForegroundProperty, value); }
    }
    public static readonly DependencyProperty ButtonInactiveForegroundProperty =
        DependencyProperty.Register(nameof(ButtonInactiveForeground), typeof(SolidColorBrush), typeof(TitleBar), new PropertyMetadata(null));

    public SolidColorBrush ButtonHoverBackground
    {
        get { return (SolidColorBrush)GetValue(ButtonHoverBackgroundProperty); }
        set { SetValue(ButtonHoverBackgroundProperty, value); }
    }
    public static readonly DependencyProperty ButtonHoverBackgroundProperty =
        DependencyProperty.Register(nameof(ButtonHoverBackground), typeof(SolidColorBrush), typeof(TitleBar), new PropertyMetadata(null));

    public SolidColorBrush ButtonHoverForeground
    {
        get { return (SolidColorBrush)GetValue(ButtonHoverForegroundProperty); }
        set { SetValue(ButtonHoverForegroundProperty, value); }
    }
    public static readonly DependencyProperty ButtonHoverForegroundProperty =
        DependencyProperty.Register(nameof(ButtonHoverForeground), typeof(SolidColorBrush), typeof(TitleBar), new PropertyMetadata(null));

    public SolidColorBrush ButtonPressedBackground
    {
        get { return (SolidColorBrush)GetValue(ButtonPressedBackgroundProperty); }
        set { SetValue(ButtonPressedBackgroundProperty, value); }
    }
    public static readonly DependencyProperty ButtonPressedBackgroundProperty =
        DependencyProperty.Register(nameof(ButtonPressedBackground), typeof(SolidColorBrush), typeof(TitleBar), new PropertyMetadata(null));

    public SolidColorBrush ButtonPressedForeground
    {
        get { return (SolidColorBrush)GetValue(ButtonPressedForegroundProperty); }
        set { SetValue(ButtonPressedForegroundProperty, value); }
    }
    public static readonly DependencyProperty ButtonPressedForegroundProperty =
        DependencyProperty.Register(nameof(ButtonPressedForeground), typeof(SolidColorBrush), typeof(TitleBar), new PropertyMetadata(null));
}
