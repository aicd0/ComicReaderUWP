// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace ComicReader.Views.Pages.Reader;

public sealed partial class PageDemoDual : UserControl
{
    public PageDemoDual()
    {
        this.InitializeComponent();
    }

    public string HeaderL
    {
        get
        {
            return (string)GetValue(HeaderLProperty);
        }
        set
        {
            SetValue(HeaderLProperty, value);
        }
    }
    public static readonly DependencyProperty HeaderLProperty =
        DependencyProperty.Register(nameof(HeaderL), typeof(string), typeof(PageDemoDual), new PropertyMetadata(null));

    public string HeaderR
    {
        get
        {
            return (string)GetValue(HeaderRProperty);
        }
        set
        {
            SetValue(HeaderRProperty, value);
        }
    }
    public static readonly DependencyProperty HeaderRProperty =
        DependencyProperty.Register(nameof(HeaderR), typeof(string), typeof(PageDemoDual), new PropertyMetadata(null));

    private bool m_IsHighlight = false;
    public bool IsHighlight
    {
        get
        {
            return m_IsHighlight;
        }
        set
        {
            m_IsHighlight = value;
            if (RootGrid != null)
            {
                if (value)
                {
                    RootGrid.Background = (SolidColorBrush)Application.Current.Resources["PageDemoBrush"];
                }
                else
                {
                    RootGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF));
                }
            }
        }
    }
}
