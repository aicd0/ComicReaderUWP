// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Core.Common.DebugTools;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.UserControls.Misc;

internal sealed partial class SearchNavigationBar : BaseUserControl
{
    public delegate void SearchTextChangedEventHandler(string text);
    public event SearchTextChangedEventHandler? SearchTextChange;

    public delegate void SearchTextSubmittedEventHandler(string text);
    public event SearchTextSubmittedEventHandler? SearchTextSubmitted;

    public SearchNavigationBar()
    {
        InitializeComponent();
    }

    public void SetSearchBox(string keywords)
    {
        SearchBox.Focus(FocusState.Programmatic);
        SearchBox.Text = keywords;
    }

    private void OnSearchBoxTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        SearchTextChange?.Invoke(sender.Text);
    }

    private void OnSearchBoxQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        string queryText = args.QueryText;

        if (DebugCommand.TryExecute(queryText))
        {
            return;
        }

        SearchTextSubmitted?.Invoke(queryText);
    }
}
