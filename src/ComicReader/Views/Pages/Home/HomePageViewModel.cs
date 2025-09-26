// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Actions;
using ComicReader.Common.Lifecycle;
using ComicReader.Common.Threading;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.Algorithm;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Threading;
using ComicReader.UserControls.ComicItemView;
using ComicReader.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Pages.Home;

internal partial class HomePageViewModel : INotifyPropertyChanged
{
    private const string TAG = nameof(HomePageViewModel);

    public event PropertyChangedEventHandler? PropertyChanged;

    public readonly MutableLiveData<Route> OpenInCurrentTabLiveData = new();
    public readonly MutableLiveData<Route> OpenInNewTabLiveData = new();
    public readonly MutableLiveData<List<ComicModel>> EditComicLiveData = new();
    public readonly MutableLiveData<FilterModel> FilterLiveData = new();
    public readonly MutableLiveData<bool> GroupingEnabledLiveData = new();
    public readonly MutableLiveData<ComicFilterModel.ViewTypeEnum> ViewTypeLiveData = new();

    public ObservableCollection<ComicItemViewModel> UngroupedComicItems { get; set; } = [];
    public ObservableCollection<ComicGroupViewModel> GroupedComicItems { get; set; } = [];

    private bool _libraryEmptyVisible = false;
    public bool LibraryEmptyVisible
    {
        get => _libraryEmptyVisible;
        set
        {
            _libraryEmptyVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LibraryEmptyVisible)));
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

    private ListViewSelectionMode _comicItemSelectionMode = ListViewSelectionMode.None;
    public ListViewSelectionMode ComicItemSelectionMode
    {
        get => _comicItemSelectionMode;
        set
        {
            _comicItemSelectionMode = value;
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

    private bool _isCommandBarFavoriteEnabled = false;
    public bool IsCommandBarFavoriteEnabled
    {
        get => _isCommandBarFavoriteEnabled;
        set
        {
            _isCommandBarFavoriteEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarFavoriteEnabled)));
        }
    }

    private bool _isCommandBarUnFavoriteEnabled = false;
    public bool IsCommandBarUnFavoriteEnabled
    {
        get => _isCommandBarUnFavoriteEnabled;
        set
        {
            _isCommandBarUnFavoriteEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarUnFavoriteEnabled)));
        }
    }

    private bool _isCommandBarHideEnabled = false;
    public bool IsCommandBarHideEnabled
    {
        get => _isCommandBarHideEnabled;
        set
        {
            _isCommandBarHideEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarHideEnabled)));
        }
    }

    private bool _isCommandBarUnHideEnabled = false;
    public bool IsCommandBarUnHideEnabled
    {
        get => _isCommandBarUnHideEnabled;
        set
        {
            _isCommandBarUnHideEnabled = value;
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

    private bool _isCollapseAllEnabled = false;
    public bool IsCollapseAllEnabled
    {
        get => _isCollapseAllEnabled;
        set
        {
            _isCollapseAllEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCollapseAllEnabled)));
        }
    }

    private bool _isExpandAllEnabled = false;
    public bool IsExpandAllEnabled
    {
        get => _isExpandAllEnabled;
        set
        {
            _isExpandAllEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpandAllEnabled)));
        }
    }

    private ActionHandler _actionHandler = ActionHandler.Dummy;
    private readonly ComicSearchEngine _searchEngine = new();
    private ComicFilterModel.ExternalModel _filterModel = new();
    private readonly ReaderWriterLock _comicItemsLock = new();
    private readonly List<ComicItemViewModel> _comicItems = [];
    private readonly List<ComicItemViewModel> _selectedComicItems = [];
    private long _lastSearchTime = 0;

    private readonly ITaskDispatcher _sharedDispatcher = TaskDispatcher.DefaultQueue;
    private bool _filterInvalidated = true;
    private bool _comicInvalidated = true;
    private int _updateFilterSubmitted = 0;
    private int _updateComicSubmitted = 0;

    private readonly List<ComicFilterModel.ViewTypeEnum> _viewTypes = [
        ComicFilterModel.ViewTypeEnum.Large,
        ComicFilterModel.ViewTypeEnum.Medium,
    ];

    /// <summary>
    /// Initializes the view model.
    /// </summary>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void Initialize(ActionHandler actionHandler)
    {
        _actionHandler = actionHandler;
        _searchEngine.SetResultCallback(OnComicSearchResult);
    }

    /// <summary>
    /// Reloads and applies the current filters.
    /// </summary>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void Refresh(bool filters = false, bool library = false)
    {
        if (filters)
        {
            _filterInvalidated = true;
        }

        if (library)
        {
            _comicInvalidated = true;
        }

        if (filters)
        {
            ScheduleUpdateFilters(true);
            return;
        }

        if (library)
        {
            ScheduleUpdateComics();
            return;
        }
    }

    /// <summary>
    /// Search the comics by keywords.
    /// </summary>
    /// <param name="searchText">The search text.</param>
    public void SetSearchText(string searchText)
    {
        searchText = searchText.Trim();
        if (searchText == _searchEngine.SearchText)
        {
            return;
        }

        _searchEngine.SearchText = searchText;

        long tick = GetTick();
        int timeRemain = 200 - (int)(tick - _lastSearchTime);
        if (timeRemain <= 0)
        {
            _lastSearchTime = tick;
            ScheduleUpdateComics();
        }
        else
        {
            _ = Task.Delay(timeRemain).ContinueWith((_) =>
            {
                _lastSearchTime = GetTick();
                ScheduleUpdateComics();
            });
        }
    }

    /// <summary>
    /// Gets the current filter model.
    /// </summary>
    /// <returns>The current <see cref="ComicFilterModel.ExternalFilterModel"/>.</returns>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public async Task<ComicFilterModel.ExternalFilterModel> GetFilter()
    {
        return await ThreadingUtils.Submit(_sharedDispatcher, "GetFilter", () =>
        {
            return _filterModel?.LastFilter?.Clone() ?? new();
        });
    }

    /// <summary>
    /// Selects the view type for displaying comics.
    /// </summary>
    /// <param name="viewType">The view type to select.</param>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void SelectViewType(ComicFilterModel.ViewTypeEnum viewType)
    {
        _sharedDispatcher.Submit("SelectViewType", delegate
        {
            bool modified = false;
            ComicFilterModel.ExternalFilterModel lastFilter = EnsureLastFilterNoLock();
            if (lastFilter.ViewType != viewType)
            {
                modified = lastFilter.SaveViewConfig;
                lastFilter.ViewType = viewType;
            }

            if (modified)
            {
                _filterModel.LastFilterModified = true;
            }

            ScheduleUpdateFilters(false);
        });
    }

    /// <summary>
    /// Selects the sorting method for comics.
    /// </summary>
    /// <param name="model">The sorting model to apply.</param>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void SelectSortOrGroup(SortByUIModel? model)
    {
        if (model == null)
        {
            return;
        }

        _sharedDispatcher.Submit("SelectSortOrGroup", delegate
        {
            bool modified = false;
            ComicFilterModel.ExternalFilterModel lastFilter = EnsureLastFilterNoLock();
            if (model.IsSortBy)
            {
                switch (model.Type)
                {
                    case SortByMenuItemTypeEnum.Property:
                        if (model.Property != null && !model.Property.Equals(lastFilter.SortBy))
                        {
                            modified = true;
                            lastFilter.SortBy = model.Property;
                        }
                        break;
                    case SortByMenuItemTypeEnum.Ascending:
                        if (!lastFilter.SortByAscending)
                        {
                            modified = true;
                            lastFilter.SortByAscending = true;
                        }
                        break;
                    case SortByMenuItemTypeEnum.Descending:
                        if (lastFilter.SortByAscending)
                        {
                            modified = true;
                            lastFilter.SortByAscending = false;
                        }
                        break;
                    case SortByMenuItemTypeEnum.Function:
                        break;
                    default:
                        break;
                }
            }
            else
            {
                switch (model.Type)
                {
                    case SortByMenuItemTypeEnum.Property:
                        if ((model.Property != null && !model.Property.Equals(lastFilter.GroupBy)) ||
                            (lastFilter.GroupBy != null && !lastFilter.GroupBy.Equals(model.Property)))
                        {
                            modified = true;
                            lastFilter.GroupBy = model.Property;
                        }
                        break;
                    case SortByMenuItemTypeEnum.Ascending:
                        if (!lastFilter.GroupByAscending)
                        {
                            modified = true;
                            lastFilter.GroupByAscending = true;
                        }
                        break;
                    case SortByMenuItemTypeEnum.Descending:
                        if (lastFilter.GroupByAscending)
                        {
                            modified = true;
                            lastFilter.GroupByAscending = false;
                        }
                        break;
                    case SortByMenuItemTypeEnum.Function:
                        if (lastFilter.GroupSortingFunction != model.FunctionType)
                        {
                            modified = true;
                            lastFilter.GroupSortingFunction = model.FunctionType;
                        }
                        if (model.Property != null && !model.Property.Equals(lastFilter.GroupSortingProperty))
                        {
                            modified = true;
                            lastFilter.GroupSortingProperty = model.Property;
                        }
                        break;
                    default:
                        break;
                }
            }

            if (modified)
            {
                _filterModel.LastFilterModified = true;
            }

            ScheduleUpdateFilters(false);
        });
    }

    /// <summary>
    /// Selects a filter preset by name.
    /// </summary>
    /// <param name="name">The name of the filter preset.</param>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void SelectFilterPreset(string? name)
    {
        name ??= "";
        _sharedDispatcher.Submit("SelectFilterPreset", delegate
        {
            ComicFilterModel.ExternalFilterModel? filter = _filterModel.Filters.Find(x => x.Name == name);
            if (filter == null)
            {
                return;
            }

            ComicFilterModel.ExternalFilterModel? lastFilter = _filterModel.LastFilter;
            filter = filter.Clone();
            _filterModel.LastFilter = filter;
            if (lastFilter is not null && !filter.SaveViewConfig)
            {
                filter.ViewType = lastFilter.ViewType;
            }

            _filterModel.LastFilterModified = false;
            ScheduleUpdateFilters(false);
        });
    }

    /// <summary>
    /// Set the selection mode.
    /// </summary>
    /// <param name="enabled">Is selection mode enabled.</param>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void SetSelectionMode(bool enabled)
    {
        if (IsSelectMode == enabled)
        {
            return;
        }
        IsSelectMode = enabled;
        ComicItemSelectionMode = enabled ? ListViewSelectionMode.Multiple : ListViewSelectionMode.None;
        if (enabled)
        {
            _selectedComicItems.Clear();
            UpdateCommandBarButtonStates();
        }
    }

    /// <summary>
    /// Sets the selected comic items.
    /// </summary>
    /// <param name="items">The selected comic items.</param>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void SetSelection(List<ComicItemViewModel> items)
    {
        _selectedComicItems.Clear();
        _selectedComicItems.AddRange(items);
        UpdateCommandBarButtonStates();
    }

    /// <summary>
    /// Gets the selected comic items according to the triggering comic item.
    /// </summary>
    /// <param name="triggerItem">The triggering comic item.</param>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public List<ComicItemViewModel> GetSelection(ComicItemViewModel triggerItem)
    {
        List<ComicItemViewModel> selection = [];
        if (_isSelectMode)
        {
            bool contained = false;
            foreach (ComicItemViewModel item in _selectedComicItems)
            {
                if (triggerItem.Comic == item.Comic)
                {
                    contained = true;
                    break;
                }
            }
            if (contained)
            {
                selection.AddRange(_selectedComicItems);
            }
            else
            {
                selection.Add(triggerItem);
            }
        }
        else
        {
            selection.Add(triggerItem);
        }
        return selection;
    }

    /// <summary>
    /// Applies an operation to a specific comic item.
    /// </summary>
    /// <param name="operationType">The operation type.</param>
    /// <param name="comic">The comic item to operate.</param>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void ApplyOperationToComic(ComicOperationType operationType, ComicItemViewModel comic)
    {
        List<ComicItemViewModel> selection = GetSelection(comic);
        _sharedDispatcher.Submit("ApplyOperationToSelection", delegate
        {
            BatchApplyOperation(operationType, selection);
        });
    }

    /// <summary>
    /// Applies a batch operation to the selected comic items.
    /// </summary>
    /// <param name="operationType">The operation type.</param>
    /// <remarks>
    /// Must be called on the UI thread.
    /// </remarks>
    public void ApplyOperationToSelection(ComicOperationType operationType)
    {
        List<ComicItemViewModel> selectedItems = [.. _selectedComicItems];
        _sharedDispatcher.Submit("ApplyOperationToSelection", delegate
        {
            BatchApplyOperation(operationType, selectedItems);
        });
    }

    /// <summary>
    /// Retrieves a random comic from the collection.
    /// </summary>
    /// <remarks>If the collection is empty, the method returns <see langword="null"/>. This method is
    /// thread-safe and can be called concurrently from multiple threads.</remarks>
    /// <returns>A <see cref="ComicModel"/> representing a random comic from the collection, or <see langword="null"/> if the
    /// collection is empty.</returns>
    public ComicModel? GetRandomComic()
    {
        _comicItemsLock.AcquireReaderLock(Timeout.Infinite);
        try
        {
            if (_comicItems.Count == 0)
            {
                return null;
            }

            int index = Random.Shared.Next(_comicItems.Count);
            return _comicItems[index].Comic;
        }
        finally
        {
            _comicItemsLock.ReleaseReaderLock();
        }
    }

    /// <summary>
    /// Toggles the collapsed state of the specified comic group.
    /// </summary>
    /// <remarks>This method inverts the current collapsed state of the provided comic group.  It also updates
    /// the state of the collapse/expand button to reflect the new state.</remarks>
    /// <param name="groupModel">The comic group to be collapsed or expanded. Cannot be null.</param>
    public void CollapseOrExpandGroup(ComicGroupViewModel groupModel)
    {
        groupModel.Collapsed = !groupModel.Collapsed;
        UpdateCollapseExpandGroupButtonStates();
    }

    /// <summary>
    /// Collapses all comic groups in the collection.
    /// </summary>
    /// <remarks>This method sets the <see cref="ComicGroupViewModel.Collapsed"/> property to <see
    /// langword="true"/> for each group in the <c>GroupedComicItems</c> collection. It also updates the state of the
    /// collapse/expand group button to reflect the changes.</remarks>
    public void CollapseAllGroups()
    {
        foreach (ComicGroupViewModel group in GroupedComicItems)
        {
            group.Collapsed = true;
        }

        UpdateCollapseExpandGroupButtonStates();
    }

    /// <summary>
    /// Expands all comic groups by setting their collapsed state to false.
    /// </summary>
    /// <remarks>This method iterates through all comic groups and expands them, ensuring that each group's
    /// <see cref="ComicGroupViewModel.Collapsed"/> property is set to <see langword="false"/>. After expanding the
    /// groups, it updates the state of the collapse/expand button to reflect the changes.</remarks>
    public void ExpandAllGroups()
    {
        foreach (ComicGroupViewModel group in GroupedComicItems)
        {
            group.Collapsed = false;
        }

        UpdateCollapseExpandGroupButtonStates();
    }

    //
    // Task Scheduler
    //

    private void ScheduleUpdateFilters(bool reloadFromDatabase)
    {
        if (Interlocked.CompareExchange(ref _updateFilterSubmitted, 1, 0) == 1)
        {
            return;
        }

        _filterInvalidated = false;
        _sharedDispatcher.Submit("ScheduleUpdateFilters", delegate
        {
            Interlocked.Exchange(ref _updateFilterSubmitted, 0);
            UpdateFiltersNoLock(reloadFromDatabase).Wait();
        });
    }

    private void ScheduleUpdateComics()
    {
        if (_filterInvalidated)
        {
            ScheduleUpdateFilters(true);
            return;
        }

        _comicInvalidated = false;
        _searchEngine.Update();
    }

    private void ScheduleDisplayComics()
    {
        if (_filterInvalidated)
        {
            ScheduleUpdateFilters(true);
            return;
        }

        if (_comicInvalidated)
        {
            ScheduleUpdateComics();
            return;
        }

        if (Interlocked.CompareExchange(ref _updateComicSubmitted, 1, 0) == 1)
        {
            return;
        }

        _sharedDispatcher.Submit("ScheduleDisplayComics", delegate
        {
            Interlocked.Exchange(ref _updateComicSubmitted, 0);
            DisplayComicsNoLock().Wait();
        });
    }

    //
    // Unsorted
    //

    private void OnComicSearchResult(IReadOnlyList<ComicModel> items)
    {
        _sharedDispatcher.Submit("OnComicSearchResult", delegate
        {
            _comicItemsLock.AcquireWriterLock(Timeout.Infinite);
            try
            {
                _comicItems.Clear();
                foreach (ComicModel item in items)
                {
                    var model = new ComicItemViewModel(item);
                    model.UpdateProgress(true);
                    model.MenuFlyoutItems = MenuFlyoutItemsCreator.CreateMenuItems(item, _actionHandler,
                        new ComicItemMenuFlyoutHandler(this, model), supportSelection: true);

                    model.OnClick = () =>
                    {
                        if (!IsSelectMode)
                        {
                            Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                                .WithParam(RouterConstants.ARG_COMIC_ID, item.Id.ToString());
                            OpenInCurrentTabLiveData.Emit(route);
                        }
                    };

                    _comicItems.Add(model);
                }
            }
            finally
            {
                _comicItemsLock.ReleaseWriterLock();
            }

            ScheduleDisplayComics();
        });
    }

    private void BatchApplyOperation(ComicOperationType operationType, List<ComicItemViewModel> models)
    {
        switch (operationType)
        {
            case ComicOperationType.Favorite:
                {
                    List<ComicItemViewModel> items = models.FindAll(x => !x.IsFavorite);
                    FavoriteModel.Instance.BatchAdd(items.ConvertAll(x => new FavoriteModel.FavoriteItem
                    {
                        Id = x.Comic.Id,
                        Title = x.Comic.Title,
                    }));
                }
                break;
            case ComicOperationType.Unfavorite:
                {
                    List<ComicItemViewModel> items = models.FindAll(x => x.IsFavorite);
                    FavoriteModel.Instance.BatchRemoveWithId(items.ConvertAll(x => x.Comic.Id));
                }
                break;
            case ComicOperationType.Hide:
                {
                    List<ComicItemViewModel> items = models.FindAll(x => !x.IsHide);
                    foreach (ComicItemViewModel item in items)
                    {
                        item.Comic.SaveHiddenAsync(true).Wait();
                    }
                }
                break;
            case ComicOperationType.Unhide:
                {
                    List<ComicItemViewModel> items = models.FindAll(x => x.IsHide);
                    foreach (ComicItemViewModel item in items)
                    {
                        item.Comic.SaveHiddenAsync(false).Wait();
                    }
                }
                break;
            case ComicOperationType.MarkAsRead:
                {
                    List<ComicItemViewModel> items = models.FindAll(x => !x.IsRead);
                    foreach (ComicItemViewModel item in items)
                    {
                        item.Comic.SetCompletionStateToCompleted().Wait();
                    }
                }
                break;
            case ComicOperationType.MarkAsReading:
                {
                    List<ComicItemViewModel> items = models.FindAll(x => !x.IsReading);
                    foreach (ComicItemViewModel item in items)
                    {
                        item.Comic.SetCompletionStateToStarted().Wait();
                    }
                }
                break;
            case ComicOperationType.MarkAsUnread:
                {
                    List<ComicItemViewModel> items = models.FindAll(x => !x.IsUnread);
                    foreach (ComicItemViewModel item in items)
                    {
                        item.Comic.SetCompletionStateToNotStarted().Wait();
                    }
                }
                break;
            default:
                break;
        }
    }

    private void UpdateCommandBarButtonStates()
    {
        int itemCount;
        _comicItemsLock.AcquireReaderLock(Timeout.Infinite);
        try
        {
            itemCount = _comicItems.Count;
        }
        finally
        {
            _comicItemsLock.ReleaseReaderLock();
        }

        bool allSelected = _selectedComicItems.Count == itemCount;
        bool favoriteEnabled = false;
        bool unfavoriteEnabled = false;
        bool hideEnabled = false;
        bool unhideEnabled = false;
        bool markAsReadEnabled = false;
        bool markAsReadingEnabled = false;
        bool markAsUnreadEnabled = false;

        foreach (ComicItemViewModel item in _selectedComicItems)
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

    private void UpdateCollapseExpandGroupButtonStates()
    {
        bool groupingEnabled = GroupingEnabledLiveData.GetValue();
        IsCollapseAllEnabled = groupingEnabled && GroupedComicItems.Count > 0 && GroupedComicItems.Any(x => !x.Collapsed);
        IsExpandAllEnabled = groupingEnabled && GroupedComicItems.Count > 0 && GroupedComicItems.Any(x => x.Collapsed);
    }

    private async Task UpdateFiltersNoLock(bool reloadFromDatabase)
    {
        Logger.I(TAG, "UpdateFiltersNoLock");

        // Validate and save
        if (reloadFromDatabase || _filterModel == null)
        {
            _filterModel = ComicFilterModel.Instance.GetModel() ?? new();
        }
        List<ComicFilterModel.ExternalFilterModel> filters = _filterModel.Filters;
        if (filters == null || filters.Count == 0)
        {
            filters = [CreateDefaultFilter()];
            _filterModel.Filters = filters;
        }
        filters.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

        ComicFilterModel.ExternalFilterModel? lastFilter = _filterModel.LastFilter ?? filters[0].Clone();
        _filterModel.LastFilter = lastFilter;

        ComicPropertyModel sortBy = lastFilter.SortBy;
        sortBy ??= new();
        lastFilter.SortBy = sortBy;

        ComicPropertyModel? groupBy = lastFilter.GroupBy;
        ComicFilterModel.Instance.UpdateModel(_filterModel);

        // Update UI
        var viewTypeDropDown = new DropDownButtonModel<ComicFilterModel.ViewTypeEnum>
        {
            Name = StringResourceProvider.Instance.ViewType,
            Items = _viewTypes.ConvertAll(x => CreateToggleMenuFlyoutItem(ViewTypeToDisplayName(x), x == lastFilter.ViewType, x)),
        };

        List<ComicPropertyModel> properties = await ComicPropertyModel.GetProperties();
        var sortByDropDown = new MenuFlyoutItemModel<SortByUIModel>
        {
            Name = StringResourceProvider.Instance.Sort,
            SubItems = CreateSortByMenuItems(properties, lastFilter.SortBy, lastFilter.SortByAscending),
        };
        var groupByDropDown = new MenuFlyoutItemModel<SortByUIModel>
        {
            Name = StringResourceProvider.Instance.Group,
            SubItems = CreateGroupByMenuItems(properties, lastFilter.GroupBy, lastFilter.GroupByAscending, lastFilter.GroupSortingFunction, lastFilter.GroupSortingProperty),
        };
        var sortAndGroupDropDown = new DropDownButtonModel<SortByUIModel>
        {
            Name = StringResourceProvider.Instance.Sort + " & " + StringResourceProvider.Instance.Group,
            Items = [sortByDropDown, groupByDropDown],
        };

        string lastFilterName = lastFilter.Name ?? "";
        if (_filterModel.LastFilterModified)
        {
            lastFilterName += " *";
        }
        var filterPresetDropDown = new DropDownButtonModel<string>
        {
            Name = lastFilterName,
            Items = filters.ConvertAll(x => CreateMenuFlyoutItem(x.Name, x.Name)),
        };

        var uiModel = new FilterModel
        {
            ViewTypeDropDown = viewTypeDropDown,
            SortAndGroupDropDown = sortAndGroupDropDown,
            FilterPresetDropDown = filterPresetDropDown,
        };
        FilterLiveData.Emit(uiModel);
        ViewTypeLiveData.Emit(lastFilter.ViewType);

        // Update comics
        if (_searchEngine.Expression == lastFilter.Expression)
        {
            ScheduleDisplayComics();
        }
        else
        {
            _searchEngine.Expression = lastFilter.Expression;
            ScheduleUpdateComics();
        }
    }

    private async Task DisplayComicsNoLock()
    {
        Logger.I(TAG, "DisplayComicsNoLock");

        IReadOnlyList<ComicItemViewModel> comicItems;
        _comicItemsLock.AcquireReaderLock(Timeout.Infinite);
        try
        {
            comicItems = [.. _comicItems];
        }
        finally
        {
            _comicItemsLock.ReleaseReaderLock();
        }

        bool isEmpty = comicItems.Count == 0;
        List<ComicItemViewModel>? comicsUngrouped = null;
        List<ComicGroupViewModel>? comicsGrouped = null;

        if (isEmpty)
        {
            comicsUngrouped = [];
        }
        else
        {
            ComicFilterModel.ExternalFilterModel filter = _filterModel.LastFilter ?? CreateDefaultFilter();
            ComicPropertyModel sortBy = filter.SortBy;
            ComicPropertyModel? groupBy = filter.GroupBy;

            if (groupBy != null)
            {
                List<ComicPropertyModel.GroupItem<ComicItemViewModel>> groupItems = groupBy.GroupComics(comicItems, (x) => x.Comic,
                    filter.GroupByAscending, filter.GroupSortingFunction, filter.GroupSortingProperty);
                comicsGrouped = [];
                foreach (ComicPropertyModel.GroupItem<ComicItemViewModel> item in groupItems)
                {
                    List<ComicItemViewModel> sorted = SortComicItemsByProerty(item.Items, sortBy, filter.SortByAscending);
                    var group = new ComicGroupViewModel(item.Name, sorted, false)
                    {
                        Description = item.Description,
                    };
                    comicsGrouped.Add(group);
                }
            }
            else
            {
                comicsUngrouped = SortComicItemsByProerty(comicItems, sortBy, filter.SortByAscending);
            }
        }

        await MainThreadUtils.RunInMainThread(delegate
        {
            bool ComicComparer(ComicItemViewModel x, ComicItemViewModel y) => x.Comic.Id == y.Comic.Id;
            void ComicUpdater(ComicItemViewModel x, ComicItemViewModel y) => x.Update(y);

            LibraryEmptyVisible = isEmpty;

            if (comicsGrouped != null)
            {
                DiffUtils.UpdateCollection(GroupedComicItems, comicsGrouped, (x, y) => x.GroupName == y.GroupName, (x, y) =>
                {
                    x.Description = y.Description;
                    x.UpdateItems(y.Items, ComicComparer, ComicUpdater);
                });

                GroupingEnabledLiveData.Emit(true);
            }
            else if (comicsUngrouped != null)
            {
                DiffUtils.UpdateCollection(UngroupedComicItems, comicsUngrouped, ComicComparer, ComicUpdater);
                GroupingEnabledLiveData.Emit(false);
            }

            UpdateCollapseExpandGroupButtonStates();
            UpdateCommandBarButtonStates();
        });
    }

    private List<ComicItemViewModel> SortComicItemsByProerty(IReadOnlyList<ComicItemViewModel> items, ComicPropertyModel property, bool ascending)
    {
        return property.SortComics(items, (x) => x.Comic, ascending);
    }

    private ComicFilterModel.ExternalFilterModel EnsureLastFilterNoLock()
    {
        ComicFilterModel.ExternalFilterModel filter = _filterModel.LastFilter ?? CreateDefaultFilter();
        _filterModel.LastFilter = filter;
        return filter;
    }

    private ComicFilterModel.ExternalFilterModel CreateDefaultFilter()
    {
        return new ComicFilterModel.ExternalFilterModel
        {
            Name = StringResourceProvider.Instance.Default,
            ViewType = ComicFilterModel.ViewTypeEnum.Large,
            SaveViewConfig = false,
            SortBy = new(),
            SortByAscending = true,
            GroupBy = null,
            GroupByAscending = true,
            Expression = "",
        };
    }

    private List<MenuFlyoutItemModel<SortByUIModel>> CreateSortByMenuItems(List<ComicPropertyModel> properties, ComicPropertyModel? selectedProperty, bool ascending)
    {
        List<MenuFlyoutItemModel<SortByUIModel>> items = [];
        items.AddRange(CreateSortByPropertyMenuItems(properties, selectedProperty, new SortByUIModel
        {
            Type = SortByMenuItemTypeEnum.Property,
            IsSortBy = true,
        }));
        if (selectedProperty != null)
        {
            items.Add(CreateSeperatorMenuFlyoutItem<SortByUIModel>());
            items.Add(CreateToggleMenuFlyoutItem(StringResourceProvider.Instance.Ascending, ascending, new SortByUIModel
            {
                Type = SortByMenuItemTypeEnum.Ascending,
                IsSortBy = true,
            }));
            items.Add(CreateToggleMenuFlyoutItem(StringResourceProvider.Instance.Descending, !ascending, new SortByUIModel
            {
                Type = SortByMenuItemTypeEnum.Descending,
                IsSortBy = true,
            }));
        }
        return items;
    }

    private List<MenuFlyoutItemModel<SortByUIModel>> CreateGroupByMenuItems(List<ComicPropertyModel> properties,
        ComicPropertyModel? selectedProperty, bool ascending, ComicFilterModel.FunctionTypeEnum sortingFunction, ComicPropertyModel? sortingProperty)
    {
        List<MenuFlyoutItemModel<SortByUIModel>> items = [];
        items.Add(CreateToggleMenuFlyoutItem(StringResourceProvider.Instance.None, selectedProperty is null, new SortByUIModel
        {
            Type = SortByMenuItemTypeEnum.Property,
            IsSortBy = false,
        }));
        items.AddRange(CreateSortByPropertyMenuItems(properties, selectedProperty, new SortByUIModel
        {
            Type = SortByMenuItemTypeEnum.Property,
            IsSortBy = false,
        }));
        if (selectedProperty != null)
        {
            items.Add(CreateSeperatorMenuFlyoutItem<SortByUIModel>());
            items.Add(CreateToggleMenuFlyoutItem(StringResourceProvider.Instance.Ascending, ascending, new SortByUIModel
            {
                Type = SortByMenuItemTypeEnum.Ascending,
                IsSortBy = false,
            }));
            items.Add(CreateToggleMenuFlyoutItem(StringResourceProvider.Instance.Descending, !ascending, new SortByUIModel
            {
                Type = SortByMenuItemTypeEnum.Descending,
                IsSortBy = false,
            }));
            items.Add(new MenuFlyoutItemModel<SortByUIModel>
            {
                Name = StringResourceProvider.Instance.SortingFunction,
                SubItems = CreateSortingFunctionMenuItems(properties, sortingFunction, sortingProperty, new SortByUIModel
                {
                    Type = SortByMenuItemTypeEnum.Function,
                    IsSortBy = false,
                }),
            });
        }
        return items;
    }

    private List<MenuFlyoutItemModel<SortByUIModel>> CreateSortingFunctionMenuItems(List<ComicPropertyModel> properties,
        ComicFilterModel.FunctionTypeEnum sortingFunction, ComicPropertyModel? sortingProperty, SortByUIModel templateModel)
    {
        string GetFunctionDisplayName(ComicFilterModel.FunctionTypeEnum function)
        {
            return function switch
            {
                ComicFilterModel.FunctionTypeEnum.None => StringResourceProvider.Instance.None,
                ComicFilterModel.FunctionTypeEnum.ItemCount => StringResourceProvider.Instance.FunctionItemCount,
                ComicFilterModel.FunctionTypeEnum.Average => StringResourceProvider.Instance.FunctionAverage,
                ComicFilterModel.FunctionTypeEnum.Sum => StringResourceProvider.Instance.FunctionSum,
                ComicFilterModel.FunctionTypeEnum.Max => StringResourceProvider.Instance.FunctionMax,
                ComicFilterModel.FunctionTypeEnum.Min => StringResourceProvider.Instance.FunctionMin,
                _ => "Unknown function"
            };
        }

        List<MenuFlyoutItemModel<SortByUIModel>> items = [];
        List<ComicFilterModel.FunctionTypeEnum> simpleFunctions = [
            ComicFilterModel.FunctionTypeEnum.None,
            ComicFilterModel.FunctionTypeEnum.ItemCount,
        ];
        List<ComicFilterModel.FunctionTypeEnum> propertyFunctions = [
            ComicFilterModel.FunctionTypeEnum.Average,
            ComicFilterModel.FunctionTypeEnum.Sum,
            ComicFilterModel.FunctionTypeEnum.Max,
            ComicFilterModel.FunctionTypeEnum.Min,
        ];
        foreach (ComicFilterModel.FunctionTypeEnum function in simpleFunctions)
        {
            items.Add(CreateToggleMenuFlyoutItem(GetFunctionDisplayName(function), function == sortingFunction, new SortByUIModel(templateModel)
            {
                FunctionType = function,
            }));
        }
        foreach (ComicFilterModel.FunctionTypeEnum function in propertyFunctions)
        {
            List<MenuFlyoutItemModel<SortByUIModel>> subItems = CreateSortByPropertyMenuItems(properties, function == sortingFunction ? sortingProperty : null, new SortByUIModel(templateModel)
            {
                FunctionType = function,
            });
            items.Add(new MenuFlyoutItemModel<SortByUIModel>
            {
                Name = GetFunctionDisplayName(function),
                SubItems = subItems,
            });
        }
        return items;
    }

    private List<MenuFlyoutItemModel<SortByUIModel>> CreateSortByPropertyMenuItems(List<ComicPropertyModel> properties,
        ComicPropertyModel? selectedProperty, SortByUIModel templateModel)
    {
        Dictionary<string, List<ComicPropertyModel>> propertyGroupMap = [];
        foreach (ComicPropertyModel property in properties)
        {
            string groupName = property.DisplayGroupName;
            if (!propertyGroupMap.TryGetValue(groupName, out List<ComicPropertyModel>? value))
            {
                value = [];
                propertyGroupMap[groupName] = value;
            }
            value.Add(property);
        }

        List<ComicPropertyModel>? plainProperties = null;
        List<KeyValuePair<string, List<ComicPropertyModel>>> propertyGroupList = [];
        foreach (KeyValuePair<string, List<ComicPropertyModel>> kvp in propertyGroupMap)
        {
            if (kvp.Key == "")
            {
                plainProperties = kvp.Value;
                continue;
            }
            propertyGroupList.Add(kvp);
            kvp.Value.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName));
        }
        propertyGroupList.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));

        List<MenuFlyoutItemModel<SortByUIModel>> items = [];
        if (plainProperties != null)
        {
            foreach (ComicPropertyModel p in plainProperties)
            {
                items.Add(CreateToggleMenuFlyoutItem(p.DisplayName, p.Equals(selectedProperty), new SortByUIModel(templateModel)
                {
                    Property = p,
                }));
            }
        }
        foreach (KeyValuePair<string, List<ComicPropertyModel>> kvp in propertyGroupList)
        {
            List<MenuFlyoutItemModel<SortByUIModel>> subItems = [];
            foreach (ComicPropertyModel p in kvp.Value)
            {
                subItems.Add(CreateToggleMenuFlyoutItem(p.DisplayName, p.Equals(selectedProperty), new SortByUIModel(templateModel)
                {
                    Property = p,
                }));
            }
            items.Add(new MenuFlyoutItemModel<SortByUIModel>
            {
                Name = kvp.Key,
                SubItems = subItems,
            });
        }
        return items;
    }

    private MenuFlyoutItemModel<T> CreateMenuFlyoutItem<T>(string name, T dataContext)
    {
        return new MenuFlyoutItemModel<T>
        {
            Name = name,
            DataContext = dataContext
        };
    }

    private MenuFlyoutItemModel<T> CreateToggleMenuFlyoutItem<T>(string name, bool toggled, T dataContext)
    {
        return new MenuFlyoutItemModel<T>
        {
            Name = name,
            CanToggle = true,
            Toggled = toggled,
            DataContext = dataContext
        };
    }

    private MenuFlyoutItemModel<T> CreateSeperatorMenuFlyoutItem<T>()
    {
        return new MenuFlyoutItemModel<T>
        {
            IsSeperator = true,
        };
    }

    private string ViewTypeToDisplayName(ComicFilterModel.ViewTypeEnum viewType)
    {
        return viewType switch
        {
            ComicFilterModel.ViewTypeEnum.Large => StringResourceProvider.Instance.ViewTypeLarge,
            ComicFilterModel.ViewTypeEnum.Medium => StringResourceProvider.Instance.ViewTypeMedium,
            _ => "Unknown"
        };
    }

    private static long GetTick()
    {
        return Environment.TickCount64;
    }

    public class FilterModel
    {
        public DropDownButtonModel<ComicFilterModel.ViewTypeEnum> ViewTypeDropDown { get; set; } = new();
        public DropDownButtonModel<SortByUIModel> SortAndGroupDropDown { get; set; } = new();
        public DropDownButtonModel<string> FilterPresetDropDown { get; set; } = new();
    }

    public class DropDownButtonModel<T>
    {
        public string Name { get; set; } = "";
        public List<MenuFlyoutItemModel<T>> Items { get; set; } = [];
    }

    public class MenuFlyoutItemModel<T>
    {
        public bool IsSeperator { get; set; } = false;
        public string Name { get; set; } = "";
        public bool CanToggle { get; set; } = false;
        public bool Toggled { get; set; } = false;
        public List<MenuFlyoutItemModel<T>>? SubItems { get; set; } = null;
        public T? DataContext { get; set; } = default;
    }

    public class SortByUIModel
    {
        public SortByUIModel() { }

        public SortByUIModel(SortByUIModel source)
        {
            IsSortBy = source.IsSortBy;
            Type = source.Type;
            FunctionType = source.FunctionType;
            Property = source.Property;
        }

        public bool IsSortBy { get; set; }
        public SortByMenuItemTypeEnum Type { get; set; }
        public ComicFilterModel.FunctionTypeEnum FunctionType { get; set; } = ComicFilterModel.FunctionTypeEnum.None;
        public ComicPropertyModel? Property { get; set; }
    }

    public enum SortByMenuItemTypeEnum
    {
        Property,
        Ascending,
        Descending,
        Function,
    }

    private class ComicItemMenuFlyoutHandler(HomePageViewModel viewModel, ComicItemViewModel item) : IComicItemMenuFlyoutHandler
    {
        void IComicItemMenuFlyoutHandler.OnOpenInNewTabClicked()
        {
            Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                .WithParam(RouterConstants.ARG_COMIC_ID, item.Comic.Id.ToString());
            viewModel.OpenInNewTabLiveData.Emit(route);
        }

        void IComicItemMenuFlyoutHandler.OnAddToFavoritesClicked()
        {
            viewModel.ApplyOperationToComic(ComicOperationType.Favorite, item);
        }

        void IComicItemMenuFlyoutHandler.OnRemoveFromFavoritesClicked()
        {
            viewModel.ApplyOperationToComic(ComicOperationType.Unfavorite, item);
        }

        void IComicItemMenuFlyoutHandler.OnHideClicked()
        {
            viewModel.ApplyOperationToComic(ComicOperationType.Hide, item);
        }

        void IComicItemMenuFlyoutHandler.OnUnhideClicked()
        {
            viewModel.ApplyOperationToComic(ComicOperationType.Unhide, item);
        }

        void IComicItemMenuFlyoutHandler.OnMarkAsReadClicked()
        {
            viewModel.ApplyOperationToComic(ComicOperationType.MarkAsRead, item);
        }

        void IComicItemMenuFlyoutHandler.OnMarkAsReadingClicked()
        {
            viewModel.ApplyOperationToComic(ComicOperationType.MarkAsReading, item);
        }

        void IComicItemMenuFlyoutHandler.OnMarkAsUnreadClicked()
        {
            viewModel.ApplyOperationToComic(ComicOperationType.MarkAsUnread, item);
        }

        void IComicItemMenuFlyoutHandler.OnEditClick()
        {
            List<ComicItemViewModel> selection = viewModel.GetSelection(item);
            viewModel.EditComicLiveData.Emit(selection.ConvertAll(x => x.Comic));
        }
    }
}
