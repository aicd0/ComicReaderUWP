// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;

using Windows.Foundation;

namespace ComicReader.UserControls.Misc;

public sealed partial class ExtendedSearchBox : UserControl
{
    private bool _hasFocus = false;

    public ExtendedSearchBox()
    {
        InitializeComponent();
    }

    public event TypedEventHandler<AutoSuggestBox, AutoSuggestBoxQuerySubmittedEventArgs> QuerySubmitted
    {
        add
        {
            SearchBox.QuerySubmitted += value;
        }
        remove
        {
            SearchBox.QuerySubmitted -= value;
        }
    }

    public event TypedEventHandler<AutoSuggestBox, AutoSuggestBoxTextChangedEventArgs> TextChanged
    {
        add
        {
            SearchBox.TextChanged += value;
        }
        remove
        {
            SearchBox.TextChanged -= value;
        }
    }

    public string Text
    {
        get
        {
            return SearchBox.Text;
        }
        set
        {
            SearchBox.Text = value;
        }
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        UpdateUI();
    }

    private void SearchBox_GotFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _hasFocus = true;
        UpdateUI();
    }

    private void SearchBox_LostFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _hasFocus = false;
        UpdateUI();
    }

    private void UpdateUI()
    {
        bool isEmpty = string.IsNullOrEmpty(SearchBox.Text);

        if (isEmpty && !_hasFocus)
        {
            SearchGlyph.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        }
        else
        {
            SearchGlyph.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        }
    }
}
