// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.BaseUI;
using ComicReader.Common.Legacy;
using ComicReader.Common.Utils;
using ComicReader.Data;
using ComicReader.Data.Models.Comic;
using ComicReader.Data.Tables;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Data.SqlHelpers;
using ComicReader.UserControls.ComicItemView;
using ComicReader.ViewModels;
using ComicReader.Views.Main;
using ComicReader.Views.Navigation;
using ComicReader.Views.Reader;

using LiteDB;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;

namespace ComicReader.Views.Search;

internal sealed partial class SearchPage : BasePage
{
    private SearchPageViewModel ViewModel { get; set; } = new SearchPageViewModel();

    private readonly IComicItemViewHandler _comicItemHandler;
    private List<Match> _matches = new();
    private int _matchIndex = 0;
    private readonly CancellationLock _searchLock = new();
    private string _keyword = "";

    public SearchPage()
    {
        InitializeComponent();
        _comicItemHandler = new ComicItemHandler(this);
    }

    //
    // Lifecycle
    //

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);
        _keyword = bundle.GetString(RouterConstants.ARG_KEYWORD, "");
        ViewModel.IsSelectMode = false;
        ViewModel.ComicItemSelectionMode = ListViewSelectionMode.None;

        C0.Run(async delegate
        {
            await StartSearch();
        });
    }

    protected override void OnResume()
    {
        base.OnResume();
        GetNavigationPageAbility().SetSearchBox(_keyword);
        ScrollViewer scrollViewer = SearchResultGridView.ChildrenBreadthFirst().OfType<ScrollViewer>().First();
        scrollViewer.ViewChanged += OnScrollViewerViewChanged;
        ObserveData();
    }

    private void ObserveData()
    {
        ViewModel.UpdateSearchResultLiveDate.Observe(this, (p1) =>
        {
            _ = StartSearch();
        });
    }

    //
    // Unsorted
    //

    private async Task StartSearch()
    {
        await _searchLock.LockAsync(async delegate (CancellationLock.Token token)
        {
            SetSelectMode(false);
            string keyword = _keyword;

            // Extract filters and keywords from string.
            var filter = Filter.Parse(keyword, out List<string> remaining);
            var keywords = new List<string>();

            foreach (string text in remaining)
            {
                keywords = keywords.Concat(text.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToList();
            }

            if (!filter.ContainsFilter("hidden"))
            {
                _ = filter.AddFilter("~hidden");
            }

            string title_text;
            string tab_title;
            string filter_brief = filter.DescriptionBrief();
            string filter_details = filter.DescriptionDetailed();

            if (keywords.Count != 0)
            {
                string keyword_combined = StringUtils.Join(" ", keywords);
                title_text = "\"" + keyword_combined + "\"";
                tab_title = StringResourceProvider.Instance.SearchResultsOf;
                tab_title = tab_title.Replace("$keyword", keyword_combined);
            }
            else if (filter_brief.Length != 0)
            {
                title_text = filter_brief;
                tab_title = filter_brief;
                filter_details = "";
            }
            else
            {
                title_text = StringResourceProvider.Instance.AllMatchedResults;
                tab_title = StringResourceProvider.Instance.SearchResults;
            }

            // update tab header
            GetMainPageAbility().SetTitle(tab_title);
            GetMainPageAbility().SetIcon(new SymbolIconSource() { Symbol = Symbol.Find });

            // start searching
            ViewModel.IsLoading = true;
            ViewModel.UpdateUI();
            await SearchMain(keywords, filter);
            _matchIndex = 0;
            ViewModel.IsLoading = false;

            // update UI
            ViewModel.Title = title_text;
            ViewModel.FilterDetails = filter_details;

            string no_results = StringResourceProvider.Instance.NoResults;
            no_results = no_results.Replace("$keyword", keyword);

            ViewModel.NoResultText = no_results;
            ViewModel.SearchResults.Clear();
        });

        await LoadMoreResults(100);
    }

    private class Match
    {
        public long Id;
        public int Similarity = 0;
        public string SortTitle = "";
    }

    private async Task SearchMain(List<string> keywords, Filter filter)
    {
        for (int i = 0; i < keywords.Count; ++i)
        {
            keywords[i] = keywords[i].ToLower();
        }

        var keyword_matched = new List<Match>();
        List<long> filter_matched = null;

        await ComicData.EnqueueCommand(delegate
        {
            var command = SelectCommand.Create(ComicTable.Instance);
            IReaderToken<long> idToken = command.PutQueryInt64(ComicTable.ColumnId);
            IReaderToken<string> title1Token = command.PutQueryString(ComicTable.ColumnTitle1);
            IReaderToken<string> title2Token = command.PutQueryString(ComicTable.ColumnTitle2);
            using SelectCommand.IReader reader = command.Execute(SqlDatabaseManager.MainDatabase);

            while (reader.Read())
            {
                // Calculate similarity.
                int similarity = 0;
                string title1 = title1Token.GetValue();
                string title2 = title2Token.GetValue();

                if (keywords.Count != 0)
                {
                    string match_text = title1 + " " + title2;
                    similarity = StringUtils.QuickMatch(keywords, match_text);

                    if (similarity < 1)
                    {
                        continue;
                    }
                }

                // Save results.
                keyword_matched.Add(new Match
                {
                    Id = idToken.GetValue(),
                    Similarity = similarity,
                    SortTitle = title1 + " " + title2
                });
            }

            var all = new List<long>(keyword_matched.Count);

            foreach (Match match in keyword_matched)
            {
                all.Add(match.Id);
            }

            filter_matched = filter.Match(all);
        }, "SearchComics");

        // Intersect two.
        _matches = C3<Match, long, long>.Intersect(keyword_matched, filter_matched,
            (Match x) => x.Id, (long x) => x,
            new C1<long>.DefaultEqualityComparer()).ToList();

        // Sort by similarity.
        _matches = _matches
            .OrderBy(delegate (Match m) { return StringUtils.SmartFileNameKeySelector(m.SortTitle); }, StringUtils.SmartFileNameComparer)
            .OrderByDescending(x => x.Similarity)
            .ToList();
    }

    private async Task LoadMoreResults(int count)
    {
        await _searchLock.LockAsync(async delegate (CancellationLock.Token token)
        {
            for (int i = 0; i < count; ++_matchIndex)
            {
                if (token.CancellationRequested)
                {
                    return;
                }

                if (_matchIndex >= _matches.Count)
                {
                    break;
                }

                Match match = _matches[_matchIndex];
                ComicModel comic = await ComicModel.FromId(match.Id, "SearchLoadComic");

                if (comic == null)
                {
                    Logger.AssertNotReachHere("A86F0459678D1B60");
                    continue;
                }

                ComicItemViewModel item = new(comic);
                item.UpdateProgress(false);
                item.Detail = "#" + comic.Id;
                ViewModel.SearchResults.Add(item);
                ++i;
            }

            ViewModel.UpdateUI();
        });
    }

    private void OnComicItemTapped(object sender, TappedRoutedEventArgs e)
    {
        if (!CanHandleTapped())
        {
            return;
        }

        if (ViewModel.IsSelectMode)
        {
            return;
        }

        C0.Run(async delegate
        {
            var item = (ComicItemViewModel)((FrameworkElement)sender).DataContext;
            ComicModel comic = await ComicModel.FromId(item.Comic.Id, "SearchOpenLoadComic");
            Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                .WithParam(RouterConstants.ARG_COMIC_ID, comic.Id.ToString());
            GetMainPageAbility().OpenInCurrentTab(route);
        });
    }

    private void OnGridViewContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        var item = args.Item as ComicItemViewModel;
        var viewHolder = args.ItemContainer.ContentTemplateRoot as ComicItemHorizontal;

        if (args.InRecycleQueue)
        {
            viewHolder.Unbind();
        }
        else
        {
            viewHolder.Bind(item, _comicItemHandler);
        }
    }

    private void OnScrollViewerViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        C0.Run(async delegate
        {
            var scrollViewer = (ScrollViewer)sender;
            if (scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset < scrollViewer.ActualHeight * 0.5)
            {
                await LoadMoreResults(30);
            }
        });
    }

    private void OnOpenInNewTabClicked(object sender, RoutedEventArgs e)
    {
        var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
            .WithParam(RouterConstants.ARG_COMIC_ID, item.Comic.Id.ToString());
        GetMainPageAbility().OpenInNewTab(route);
    }

    private void OnEditComicInfoClick(ComicItemViewModel item)
    {
        List<ComicModel> selection = ViewModel.GetSelection(item).ConvertAll(x => x.Comic);
        C0.Run(async () =>
        {
            var dialog = new EditComicInfoDialog(selection);
            ContentDialogResult result = await dialog.ShowAsync(XamlRoot);
            if (result == ContentDialogResult.Primary)
            {
                await StartSearch();
            }
        });
    }

    private void SetSelectMode(bool val)
    {
        if (val == ViewModel.IsSelectMode)
        {
            return;
        }

        ViewModel.IsSelectMode = val;
        ViewModel.ComicItemSelectionMode = val ? ListViewSelectionMode.Multiple : ListViewSelectionMode.None;
    }

    private void OnSelectClicked(object sender, RoutedEventArgs e)
    {
        SetSelectMode(true);
    }

    private void OnScrollViewerTapped(object sender, TappedRoutedEventArgs e)
    {
        SetSelectMode(false);
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

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>();
    }

    private INavigationPageAbility GetNavigationPageAbility()
    {
        return GetAbility<INavigationPageAbility>();
    }

    //
    // Types
    //

    private class ComicItemHandler(SearchPage page) : IComicItemViewHandler
    {
        private readonly SearchPage _page = page;

        public void OnAddToFavoritesClicked(object sender, RoutedEventArgs e)
        {
            var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            _page.ViewModel.ApplyOperationToComic(ComicOperationType.Favorite, item);
        }

        public void OnEditClick(object sender, RoutedEventArgs e)
        {
            var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            _page.OnEditComicInfoClick(item);
        }

        public void OnHideClicked(object sender, RoutedEventArgs e)
        {
            var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            _page.ViewModel.ApplyOperationToComic(ComicOperationType.Hide, item);
        }

        public void OnItemTapped(object sender, TappedRoutedEventArgs e)
        {
            _page.OnComicItemTapped(sender, e);
        }

        public void OnMarkAsReadClicked(object sender, RoutedEventArgs e)
        {
            var item = (ComicItemViewModel)((FrameworkElement)sender).DataContext;
            _page.ViewModel.ApplyOperationToComic(ComicOperationType.MarkAsRead, item);
        }

        public void OnMarkAsReadingClicked(object sender, RoutedEventArgs e)
        {
            var item = (ComicItemViewModel)((FrameworkElement)sender).DataContext;
            _page.ViewModel.ApplyOperationToComic(ComicOperationType.MarkAsReading, item);
        }

        public void OnMarkAsUnreadClicked(object sender, RoutedEventArgs e)
        {
            var item = (ComicItemViewModel)((FrameworkElement)sender).DataContext;
            _page.ViewModel.ApplyOperationToComic(ComicOperationType.MarkAsUnread, item);
        }

        public void OnOpenInNewTabClicked(object sender, RoutedEventArgs e)
        {
            _page.OnOpenInNewTabClicked(sender, e);
        }

        public void OnRemoveFromFavoritesClicked(object sender, RoutedEventArgs e)
        {
            var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            _page.ViewModel.ApplyOperationToComic(ComicOperationType.Unfavorite, item);
        }

        public void OnSelectClicked(object sender, RoutedEventArgs e)
        {
            _page.OnSelectClicked(sender, e);
        }

        public void OnUnhideClicked(object sender, RoutedEventArgs e)
        {
            var item = (ComicItemViewModel)((MenuFlyoutItem)sender).DataContext;
            _page.ViewModel.ApplyOperationToComic(ComicOperationType.Unhide, item);
        }
    }
}
