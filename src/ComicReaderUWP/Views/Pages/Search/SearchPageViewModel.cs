// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Helpers.Misc;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.Helpers.Search;
using ComicReaderUWP.UserControls.ComicSelection;
using ComicReaderUWP.ViewModels;

namespace ComicReaderUWP.Views.Pages.Search;

internal partial class SearchPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isLoadingRingVisible;
    public bool IsLoadingRingVisible
    {
        get => _isLoadingRingVisible;
        set
        {
            _isLoadingRingVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoadingRingVisible)));
        }
    }

    private string _title = "";
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        }
    }

    private string _filterDetails = "";
    public string FilterDetails
    {
        get => _filterDetails;
        set
        {
            _filterDetails = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilterDetails)));
        }
    }

    private bool _isResultGridVisible;
    public bool IsResultGridVisible
    {
        get => _isResultGridVisible;
        set
        {
            _isResultGridVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsResultGridVisible)));
        }
    }

    private string _noResultText = string.Empty;
    public string NoResultText
    {
        get => _noResultText;
        set
        {
            _noResultText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NoResultText)));
        }
    }

    private bool _isNoResultTextVisible;
    public bool IsNoResultTextVisible
    {
        get => _isNoResultTextVisible;
        set
        {
            _isNoResultTextVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNoResultTextVisible)));
        }
    }

    public readonly MutableLiveData<IReadOnlyList<ComicItemViewModel>> ResultsLiveData = new();

    public ComicSelectionViewModel ComicSelection { get; }
    public bool IsLoading;
    public bool IsResultEmpty => SearchResults.Count == 0;
    public List<ComicItemViewModel> SearchResults { get; private set; } = [];

    private readonly ITaskDispatcher _sharedDispatcher = TaskDispatcher.DefaultQueue;
    private ActionHandler _actionHandler = ActionHandler.Dummy;
    private readonly ComicSearchEngine _searchEngine = new();

    public SearchPageViewModel()
    {
        ComicSelection = new(() => SearchResults.Count);
    }

    public void Initialize(ActionHandler actionHandler, string searchText)
    {
        _actionHandler = actionHandler;
        _searchEngine.SetResultCallback(OnSearchResult);

        IsLoading = true;
        ComicSelection.SetSelectMode(false);

        _searchEngine.SearchText = searchText;
        Refresh();
    }

    public void Refresh()
    {
        _searchEngine.Update();
    }

    public void UpdateUI()
    {
        IsLoadingRingVisible = IsLoading;
        IsResultGridVisible = !IsLoading && !IsResultEmpty;
        IsNoResultTextVisible = !IsLoading && IsResultEmpty;
    }

    private void OnSearchResult(IReadOnlyList<ComicModel> comics)
    {
        _sharedDispatcher.Submit(() =>
        {
            IEnumerable<ComicModel> sortedComics = comics.OrderBy(x => x.Title);
            List<ComicItemViewModel> newItems = [];
            PlaylistModel.Builder playlist = new PlaylistModel.Builder().AddComics(sortedComics);
            foreach (ComicModel comic in sortedComics)
            {
                ComicItemViewModel item = new(comic)
                {
                    OnClick = model =>
                    {
                        if (ComicSelection.IsSelectMode)
                        {
                            return;
                        }

                        Route route = OpenComicHelper.GetComicRoute(comic, playlist: playlist);
                        OpenComicHelper.OpenComic(_actionHandler, route);
                    },
                    OnRequestContextFlyoutAsync = model =>
                    {
                        IEnumerable<ComicModel>? selection = ComicSelection.IsSelectMode ? ComicSelection.GetSelectedComics() : null;
                        return MenuFlyoutItemsCreator.CreateComicMenuItems(
                            _actionHandler,
                            comic,
                            playlist: playlist,
                            selectedComics: selection,
                            canSelect: true);
                    },
                };
                item.UpdateProgress(false);
                newItems.Add(item);
            }

            CoroutineUtils.RunInMainThread(() =>
            {
                IsLoading = false;
                SearchResults = newItems;
                ResultsLiveData.Emit(SearchResults);
                UpdateUI();
            });
        });
    }
}
