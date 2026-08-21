// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Actions.Providers;
using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.BaseUI.PageAbilities;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.UserControls.ComicItemView;
using ComicReaderUWP.UserControls.Misc;
using ComicReaderUWP.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;

namespace ComicReaderUWP.Views.Pages.Search;

internal sealed partial class SearchPage : BasePage
{
    private const string TAG = nameof(SearchPage);

    private SearchPageViewModel ViewModel { get; set; } = new SearchPageViewModel();

    private readonly SearchNavigationBar _searchNavigationBar;
    private string _keyword = "";

    public SearchPage()
    {
        InitializeComponent();
        _searchNavigationBar = new();
    }

    //
    // Lifecycle
    //

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);

        PageActionHandler.RegisterProvider(new CustomActionProvider(new CustomActionHandler(ViewModel)));

        _keyword = bundle.GetString(RouterConstants.ARG_KEYWORD, "");

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
        GetMainPageAbility().SetCustomNavigationBar(_searchNavigationBar);

        ViewModel.Initialize(PageActionHandler, _keyword);
        ViewModel.Title = titleText;
        ViewModel.NoResultText = StringResourceProvider.Instance.NoResults.Replace("$keyword", searchText);

        ObserveData();
    }

    protected override void OnResume()
    {
        base.OnResume();

        _searchNavigationBar.SetSearchBox(_keyword);
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

        _searchNavigationBar.SearchTextSubmitted += text =>
        {
            text = text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SEARCH)
                .WithParam(RouterConstants.ARG_KEYWORD, text);
            GetMainPageAbility().OpenInCurrentTab(route);
        };
    }

    //
    // Unsorted
    //

    private void OnGridViewContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        var viewHolder = (ComicItemHorizontal)args.ItemContainer.ContentTemplateRoot;
        if (args.InRecycleQueue)
        {
            viewHolder.SetComicModel(null);
        }
        else
        {
            var item = (ComicItemViewModel)args.Item;
            viewHolder.SetComicModel(item);
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

    private void CommandBarFavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<ComicModel> comics = ViewModel.GetSelectedComics();
        FavoriteModel.Instance.BatchAdd([.. comics.Select(x => new FavoriteModel.FavoriteItem
        {
            Id = x.Id,
            Title = x.Title,
        })]);
    }

    private void CommandBarUnfavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<ComicModel> comics = ViewModel.GetSelectedComics();
        FavoriteModel.Instance.BatchRemoveWithId([.. comics.Select(x => x.Id)]);
    }

    private void CommandBarCompletionStatusButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe)
        {
            return;
        }

        IReadOnlyList<ComicModel> comics = ViewModel.GetSelectedComics();
        List<BaseMenuFlyoutItemModel> menuItems = MenuFlyoutItemsCreator.CreateCompletionStatusMenuItems(comics);

        var flyout = new MenuFlyout();
        foreach (BaseMenuFlyoutItemModel item in menuItems)
        {
            flyout.Items.Add(item.CreateMenuFlyoutItem());
        }

        flyout.ShowAt(fe, new FlyoutShowOptions { Placement = FlyoutPlacementMode.Top });
    }

    private void CommandBarHideButton_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<ComicModel> comics = ViewModel.GetSelectedComics();
        CoroutineUtils.Run(() => BusyStateManager.WithBusyState(async () =>
        {
            await Task.WhenAll(comics.Select(x => x.SetHidden(true)));
        }));
    }

    private void CommandBarUnhideButton_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<ComicModel> comics = ViewModel.GetSelectedComics();
        CoroutineUtils.Run(() => BusyStateManager.WithBusyState(async () =>
        {
            await Task.WhenAll(comics.Select(x => x.SetHidden(false)));
        }));
    }

    private void CommandBarDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<ComicModel> comics = ViewModel.GetSelectedComics();
        string idList = string.Join(',', comics.Select(x => x.Id.ToString()));
        ActionModel actionModel = ActionModel.Builder.Create(DeleteComicProvider.NAME)
            .AddParameter(DeleteComicProvider.PARAM_COMIC_ID, idList)
            .Build();
        CoroutineUtils.Run(() => BusyStateManager.WithBusyState(async () =>
        {
            await PageActionHandler.Handle(actionModel);
        }));
    }

    //
    // Utilities
    //

    private IMainPageAbilityForTab GetMainPageAbility()
    {
        return GetAbility<IMainPageAbilityForTab>()!;
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
