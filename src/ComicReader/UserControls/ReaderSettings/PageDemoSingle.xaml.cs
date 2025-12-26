// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace ComicReader.UserControls.ReaderSettings;

public sealed partial class PageDemoSingle : UserControl
{
    public PageDemoSingle()
    {
        InitializeComponent();
    }

    public string Header
    {
        get
        {
            return (string)GetValue(HeaderProperty);
        }
        set
        {
            SetValue(HeaderProperty, value);
        }
    }
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(PageDemoSingle), new PropertyMetadata(null));

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
