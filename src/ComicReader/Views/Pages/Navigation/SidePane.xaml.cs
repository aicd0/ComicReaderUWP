// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using ComicReader.Common.BaseUI;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Pages.Navigation;

internal sealed partial class SidePane : BaseUserControl
{
    public delegate void SelectionChangedEventHandler(SidePane sender, string item);
    public event SelectionChangedEventHandler SelectionChanged;

    public SidePane()
    {
        InitializeComponent();
    }

    public void Navigate(NavigationBundle bundle)
    {
        ContentFrame.Navigate(bundle.PageTrait.GetPageType(), bundle);
    }

    private void OnNavPaneSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        string item = (string)((NavigationViewItem)args.SelectedItem).Content;
        SelectionChanged?.Invoke(this, item);
    }
}
