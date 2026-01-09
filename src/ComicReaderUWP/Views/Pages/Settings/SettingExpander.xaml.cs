// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Views.Pages.Settings;

public sealed partial class SettingExpander : UserControl
{
    public SettingExpander()
    {
        this.InitializeComponent();
    }

    public string Header
    {
        get { return (string)GetValue(HeaderProperty); }
        set { SetValue(HeaderProperty, value); }
    }
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(SettingExpander), new PropertyMetadata(null));

    public string Detail
    {
        get { return (string)GetValue(DetailProperty); }
        set { SetValue(DetailProperty, value); }
    }
    public static readonly DependencyProperty DetailProperty =
        DependencyProperty.Register(nameof(Detail), typeof(string), typeof(SettingExpander), new PropertyMetadata(null));

    public string Glyph
    {
        get { return (string)GetValue(GlyphProperty); }
        set { SetValue(GlyphProperty, value); }
    }
    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(SettingExpander), new PropertyMetadata(null));

    public UIElement InnerContent
    {
        get { return (UIElement)GetValue(InnerContentProperty); }
        set { SetValue(InnerContentProperty, value); }
    }
    public static readonly DependencyProperty InnerContentProperty =
        DependencyProperty.Register(nameof(InnerContent), typeof(UIElement), typeof(SettingExpander), new PropertyMetadata(null));

    public bool IsExpanded
    {
        get { return (bool)GetValue(IsExpandedProperty); }
        set { SetValue(IsExpandedProperty, value); }
    }
    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(SettingExpander), new PropertyMetadata(false));
}
