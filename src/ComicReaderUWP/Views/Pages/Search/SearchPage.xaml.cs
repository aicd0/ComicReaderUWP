// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReaderUWP.Common.Actions.Providers;
using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.BaseUI.PageAbilities;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Utils;
using ComicReaderUWP.UserControls.ComicItemView;
using ComicReaderUWP.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;

namespace ComicReaderUWP.Views.Pages.Search;

internal sealed partial class SearchPage : BasePage
{
    private const string TAG = nameof(SearchPage);

    private SearchPageViewModel ViewModel { get; set; } = new SearchPageViewModel();

    private string _keyword = "";

    public SearchPage()
    {
        InitializeComponent();
    }

    //
    // Lifecycle
    //

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);

        PageActionHandler.RegisterProvider(new CustomActionProvider(new CustomActionHandler(ViewModel)));

        _keyword = bundle.GetString(RouterConstants.ARG_KEYWORD, "");

        ViewModel.Initialize(PageActionHandler, _keyword);

        ObserveData();

        string searchText = _keyword.Trim();
        string titleText;
        string tabTitle;
        if (searchText.Length > 0)
        {
            titleText = $"\"{searchText}\"";
            tabTitle = StringResourceProvider.Instance.SearchResultsOf.Replace("$keyword", searchText);
        }
        else
        {
            titleText = StringResourceProvider.Instance.AllMatchedResults;
            tabTitle = StringResourceProvider.Instance.SearchResults;
        }

        GetMainPageAbility().SetTitle(tabTitle);
        GetMainPageAbility().SetIcon(new SymbolIconSource() { Symbol = Symbol.Find });
        ViewModel.Title = titleText;
        ViewModel.NoResultText = StringResourceProvider.Instance.NoResults.Replace("$keyword", searchText);
    }

    protected override void OnResume()
    {
        base.OnResume();

        GetNavigationPageAbility().SetSearchBox(_keyword);
    }

    private void ObserveData()
    {
        GlobalEvent.Instance.ComicUpdated.Observe(this, (p1) =>
        {
            ViewModel.Refresh();
        });

        GlobalEvent.Instance.FavoriteUpdated.Observe(this, (p1) =>
        {
            ViewModel.Refresh();
        });
    }

    //
    // Unsorted
    //

    private void OnGridViewContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        var viewHolder = (ComicItemHorizontal)args.ItemContainer.ContentTemplateRoot;
        if (args.InRecycleQueue)
        {
            viewHolder.Unbind();
        }
        else
        {
            var item = (ComicItemViewModel)args.Item;
            viewHolder.Bind(item);
        }
    }

    private void OnScrollViewerTapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.SetSelectMode(false);
    }

    private void OnGridViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        List<ComicItemViewModel> selectedItems = [];
        foreach (object item in SearchResultGridView.SelectedItems)
        {
            if (item is ComicItemViewModel comicItem)
            {
                selectedItems.Add(comicItem);
            }
        }
        ViewModel.SetSelection(selectedItems);
    }

    private void CommandBarSelectAllClicked(object sender, RoutedEventArgs e)
    {
        var button = sender as AppBarToggleButton;
        if (button == null)
        {
            return;
        }
        if (button.IsChecked == true)
        {
            SearchResultGridView.SelectAll();
        }
        else
        {
            SearchResultGridView.DeselectRange(new ItemIndexRange(0, (uint)SearchResultGridView.Items.Count));
        }
    }

    private void CommandBarFavoriteClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplyOperationToComicSelection(ComicOperationType.Favorite);
    }

    private void CommandBarUnFavoriteClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplyOperationToComicSelection(ComicOperationType.Unfavorite);
    }

    private void CommandBarHideClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplyOperationToComicSelection(ComicOperationType.Hide);
    }

    private void CommandBarUnhideClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplyOperationToComicSelection(ComicOperationType.Unhide);
    }

    private void CommandBarMarkAsReadClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplyOperationToComicSelection(ComicOperationType.MarkAsRead);
    }

    private void CommandBarMarkAsReadingClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplyOperationToComicSelection(ComicOperationType.MarkAsReading);
    }

    private void CommandBarMarkAsUnreadClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplyOperationToComicSelection(ComicOperationType.MarkAsUnread);
    }

    //
    // Utilities
    //

    private IMainPageAbilityForTab GetMainPageAbility()
    {
        return GetAbility<IMainPageAbilityForTab>()!;
    }

    private INavigationPageAbility GetNavigationPageAbility()
    {
        return GetAbility<INavigationPageAbility>()!;
    }

    //
    // Types
    //

    private class CustomActionHandler(SearchPageViewModel viewModel) : CustomActionProvider.IHandler
    {
        public void Handle(string source, string name, IReadOnlyList<string> args)
        {
            bool handled = true;
            switch (source)
            {
                case MenuFlyoutItemsCreator.CUSTOM_ACTION_SOURCE_COMIC_ITEM_MENU:
                    switch (name)
                    {
                        case MenuFlyoutItemsCreator.CUSTOM_ACTION_NAME_SELECT:
                            viewModel.SetSelectMode(!viewModel.IsSelectMode);
                            break;
                        default:
                            handled = false;
                            break;
                    }
                    break;
                default:
                    handled = false;
                    break;
            }

            if (!handled)
            {
                Logger.F(TAG, $"Unknown action '{source}.{name}'");
            }
        }
    }
}
