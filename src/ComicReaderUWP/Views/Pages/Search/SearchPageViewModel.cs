// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Helpers.Misc;
using ComicReaderUWP.Helpers.Search;
using ComicReaderUWP.SDK.Common.Algorithm;
using ComicReaderUWP.SDK.Common.Threading;
using ComicReaderUWP.SDK.Common.Utils;
using ComicReaderUWP.UserControls.ComicItemView;
using ComicReaderUWP.ViewModels;
using ComicReaderUWP.Views.Pages.Home;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Views.Pages.Search;

internal partial class SearchPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool m_IsLoadingRingVisible;
    public bool IsLoadingRingVisible
    {
        get => m_IsLoadingRingVisible;
        set
        {
            m_IsLoadingRingVisible = value;
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

    private bool m_IsResultGridVisible;
    public bool IsResultGridVisible
    {
        get => m_IsResultGridVisible;
        set
        {
            m_IsResultGridVisible = value;
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

    private bool m_IsNoResultTextVisible;
    public bool IsNoResultTextVisible
    {
        get => m_IsNoResultTextVisible;
        set
        {
            m_IsNoResultTextVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNoResultTextVisible)));
        }
    }

    private bool _isSelectMode = false;
    public bool IsSelectMode
    {
        get => _isSelectMode;
        set
        {
            _isSelectMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{nameof(IsSelectMode)}"));
        }
    }

    private ListViewSelectionMode m_ComicItemSelectionMode = ListViewSelectionMode.None;
    public ListViewSelectionMode ComicItemSelectionMode
    {
        get => m_ComicItemSelectionMode;
        set
        {
            m_ComicItemSelectionMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{nameof(ComicItemSelectionMode)}"));
        }
    }

    private bool _isCommandBarSelectAllToggled = false;
    public bool IsCommandBarSelectAllToggled
    {
        get => _isCommandBarSelectAllToggled;
        set
        {
            _isCommandBarSelectAllToggled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarSelectAllToggled)));
        }
    }

    private bool m_IsCommandBarFavoriteEnabled = false;
    public bool IsCommandBarFavoriteEnabled
    {
        get => m_IsCommandBarFavoriteEnabled;
        set
        {
            m_IsCommandBarFavoriteEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarFavoriteEnabled)));
        }
    }

    private bool m_IsCommandBarUnFavoriteEnabled = false;
    public bool IsCommandBarUnFavoriteEnabled
    {
        get => m_IsCommandBarUnFavoriteEnabled;
        set
        {
            m_IsCommandBarUnFavoriteEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarUnFavoriteEnabled)));
        }
    }

    private bool m_IsCommandBarHideEnabled = false;
    public bool IsCommandBarHideEnabled
    {
        get => m_IsCommandBarHideEnabled;
        set
        {
            m_IsCommandBarHideEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarHideEnabled)));
        }
    }

    private bool m_IsCommandBarUnHideEnabled = false;
    public bool IsCommandBarUnHideEnabled
    {
        get => m_IsCommandBarUnHideEnabled;
        set
        {
            m_IsCommandBarUnHideEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarUnHideEnabled)));
        }
    }

    private bool _isCommandBarMarkAsReadEnabled = false;
    public bool IsCommandBarMarkAsReadEnabled
    {
        get => _isCommandBarMarkAsReadEnabled;
        set
        {
            _isCommandBarMarkAsReadEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarMarkAsReadEnabled)));
        }
    }

    private bool _isCommandBarMarkAsReadingEnabled = false;
    public bool IsCommandBarMarkAsReadingEnabled
    {
        get => _isCommandBarMarkAsReadingEnabled;
        set
        {
            _isCommandBarMarkAsReadingEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarMarkAsReadingEnabled)));
        }
    }

    private bool _isCommandBarMarkAsUnreadEnabled = false;
    public bool IsCommandBarMarkAsUnreadEnabled
    {
        get => _isCommandBarMarkAsUnreadEnabled;
        set
        {
            _isCommandBarMarkAsUnreadEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarMarkAsUnreadEnabled)));
        }
    }

    public bool IsLoading;

    public bool IsResultEmpty => SearchResults.Count == 0;

    public ObservableCollection<ComicItemViewModel> SearchResults = [];

    private readonly ITaskDispatcher _sharedDispatcher = TaskDispatcher.DefaultQueue;
    private readonly List<ComicItemViewModel> _selectedItems = [];
    private ActionHandler _actionHandler = ActionHandler.Dummy;
    private readonly ComicSearchEngine _searchEngine = new();

    public void Initialize(ActionHandler actionHandler, string searchText)
    {
        _actionHandler = actionHandler;
        _searchEngine.SetResultCallback(OnSearchResult);

        IsLoading = true;
        SetSelectMode(false);

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

    public void SetSelectMode(bool val)
    {
        if (val == IsSelectMode)
        {
            return;
        }

        IsSelectMode = val;
        ComicItemSelectionMode = val ? ListViewSelectionMode.Multiple : ListViewSelectionMode.None;
    }

    public void SetSelection(IEnumerable<ComicItemViewModel> selectedItems)
    {
        _selectedItems.Clear();
        _selectedItems.AddRange(selectedItems);
        UpdateCommandBarButtonStates();
    }

    public void ApplyOperationToComicSelection(ComicOperationType operationType)
    {
        List<ComicItemViewModel> selectedItems = [.. _selectedItems];
        CoroutineUtils.Start(() => BusyStateManager.WithBusyState(async () =>
        {
            await HomePageViewModel.BatchApplyOperation(operationType, selectedItems);
        }));
    }

    private void UpdateCommandBarButtonStates()
    {
        bool allSelected = _selectedItems.Count == SearchResults.Count;
        bool favoriteEnabled = false;
        bool unfavoriteEnabled = false;
        bool hideEnabled = false;
        bool unhideEnabled = false;
        bool markAsReadEnabled = false;
        bool markAsReadingEnabled = false;
        bool markAsUnreadEnabled = false;

        foreach (ComicItemViewModel item in _selectedItems)
        {
            if (item.IsFavorite)
            {
                unfavoriteEnabled = true;
            }
            else
            {
                favoriteEnabled = true;
            }

            if (item.IsHide)
            {
                unhideEnabled = true;
            }
            else
            {
                hideEnabled = true;
            }

            if (!item.IsRead)
            {
                markAsReadEnabled = true;
            }

            if (!item.IsReading)
            {
                markAsReadingEnabled = true;
            }

            if (!item.IsUnread)
            {
                markAsUnreadEnabled = true;
            }
        }

        IsCommandBarSelectAllToggled = allSelected;
        IsCommandBarFavoriteEnabled = favoriteEnabled;
        IsCommandBarUnFavoriteEnabled = unfavoriteEnabled;
        IsCommandBarHideEnabled = hideEnabled;
        IsCommandBarUnHideEnabled = unhideEnabled;
        IsCommandBarMarkAsReadEnabled = markAsReadEnabled;
        IsCommandBarMarkAsReadingEnabled = markAsReadingEnabled;
        IsCommandBarMarkAsUnreadEnabled = markAsUnreadEnabled;
    }

    private void OnSearchResult(IReadOnlyList<ComicModel> comics)
    {
        _sharedDispatcher.Submit("OnSearchResult", () =>
        {
            List<ComicItemViewModel> newItems = [];
            PlaylistModel.Builder playlist = PlaylistModel.Builder.Create().AddComics(comics);
            foreach (ComicModel comic in comics)
            {
                ComicItemViewModel item = new(comic)
                {
                    OnClick = model =>
                    {
                        if (_isSelectMode)
                        {
                            return;
                        }

                        OpenComicHelper.OpenComic(_actionHandler, OpenComicHelper.GetComicRoute(comic, playlist));
                    },
                    OnRequestContextFlyoutAsync = model =>
                    {
                        IEnumerable<ComicModel>? selection = _isSelectMode ? _selectedItems.Select(x => x.Comic) : null;
                        return MenuFlyoutItemsCreator.CreateComicMenuItems(
                            _actionHandler, comic, playlist,
                            selectedComics: selection, canSelect: true);
                    },
                };
                item.UpdateProgress(false);
                newItems.Add(item);
                playlist.AddComic(comic);
            }

            CoroutineUtils.RunInMainThread(() =>
            {
                bool ComicComparer(ComicItemViewModel x, ComicItemViewModel y) => x.Comic.Id == y.Comic.Id;
                void ComicUpdater(ComicItemViewModel x, ComicItemViewModel y) => x.Update(y);

                IsLoading = false;
                DiffUtils.UpdateCollection(SearchResults, newItems, ComicComparer, ComicUpdater);
                UpdateUI();
                UpdateCommandBarButtonStates();
            });
        });
    }
}
