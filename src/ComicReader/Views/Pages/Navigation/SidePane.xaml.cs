// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.BaseUI;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Pages.Navigation;

internal sealed partial class SidePane : BaseUserControl
{
    public const string FAVORITES = "Favorites";
    public const string HISTORY = "History";
    public const string TAGS = "Tags";

    public delegate void SelectionChangedEventHandler(SidePane sender, string item);
    public event SelectionChangedEventHandler? SelectionChanged;

    public SidePane()
    {
        InitializeComponent();
    }

    //
    // Public Methods
    //

    public void Navigate(NavigationBundle bundle)
    {
        ContentFrame.Navigate(bundle.PageTrait.GetPageType(), bundle);
    }

    public bool NavigateToItem(string itemName)
    {
        foreach (object item in MainNavigationView.MenuItems)
        {
            if (item is NavigationViewItem viewItem && viewItem.Name == itemName)
            {
                MainNavigationView.SelectedItem = viewItem;
                return true;
            }
        }

        return false;
    }

    //
    // Events
    //

    private void OnNavPaneSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        string item = ((NavigationViewItem)args.SelectedItem).Name;
        SelectionChanged?.Invoke(this, item);
    }
}
