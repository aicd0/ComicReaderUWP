// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Core.Common.Algorithm;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Helpers.Misc;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.Helpers.Search;
using ComicReaderUWP.UserControls.ComicItemView;
using ComicReaderUWP.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Views.Pages.Home;

internal partial class HomePageViewModel : INotifyPropertyChanged
{
    private const string TAG = nameof(HomePageViewModel);

    public event PropertyChangedEventHandler? PropertyChanged;

    public readonly MutableLiveData<string> UrlLiveData = new();
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

    private bool _isCommandBarCompletionStatusEnabled = false;
    public bool IsCommandBarCompletionStatusEnabled
    {
        get => _isCommandBarCompletionStatusEnabled;
        set
        {
            _isCommandBarCompletionStatusEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarCompletionStatusEnabled)));
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
    private ComicFilterModel.ExternalModel? _filterSettingsModel;
    private ComicFilterModel.ExternalFilterModel? _filterModel;
    private IReadOnlyList<ComicModel> _comics = [];
    private readonly List<ComicItemViewModel> _selectedComicItems = [];
    private long _lastSearchTime = 0;

    private readonly ITaskDispatcher _sharedDispatcher = TaskDispatcher.Factory.NewQueue("HomePageQueue");
    private bool _filterInvalidated = true;
    private bool _comicInvalidated = true;
    private int _updateFilterSubmitted = 0;
    private int _updateComicSubmitted = 0;

    private readonly List<ComicFilterModel.ViewTypeEnum> _viewTypes = [
        ComicFilterModel.ViewTypeEnum.Large,
        ComicFilterModel.ViewTypeEnum.Medium,
    ];

    public void Initialize(ActionHandler actionHandler, string? filterJson)
    {
        _actionHandler = actionHandler;
        _searchEngine.SetResultCallback(OnComicSearchResult);
        _filterSettingsModel = ComicFilterModel.Instance.GetModel() ?? new();

        ComicFilterModel.FilterModel? jsonModel = null;
        if (!string.IsNullOrEmpty(filterJson))
        {
            try
            {
                jsonModel = JsonSerializer.Deserialize<ComicFilterModel.FilterModel>(filterJson);
            }
            catch (JsonException ex)
            {
                Logger.E(TAG, ex);
            }
        }

        if (jsonModel is not null)
        {
            _filterModel = ComicFilterModel.ExternalFilterModel.From(jsonModel);
        }

        Refresh(filters: true, library: true);
    }

    public void Refresh(bool clearFilter = false, bool filters = false, bool library = false)
    {
        if (clearFilter)
        {
            _filterModel = null;
            _filterInvalidated = true;
        }

        if (filters)
        {
            _filterInvalidated = true;
        }

        if (library)
        {
            _comicInvalidated = true;
        }

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
    }

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
            CoroutineUtils.Run(async () =>
            {
                await Task.Delay(timeRemain);
                _lastSearchTime = GetTick();
                ScheduleUpdateComics();
            });
        }
    }

    public Task<ComicFilterModel.ExternalFilterModel> GetFilter()
    {
        return _sharedDispatcher.Submit(() =>
        {
            return _filterModel?.Clone() ?? ComicFilterModel.ExternalFilterModel.FromDefault();
        });
    }

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

    public void SetSelection(List<ComicItemViewModel> items)
    {
        _selectedComicItems.Clear();
        _selectedComicItems.AddRange(items);
        UpdateCommandBarButtonStates();
    }

    public void ApplyOperationToSelection(ComicOperationType operationType)
    {
        List<ComicItemViewModel> selectedItems = [.. _selectedComicItems];
        CoroutineUtils.Run(() => BusyStateManager.WithBusyState(async () =>
        {
            await BatchApplyOperation(operationType, selectedItems);
        }));
    }

    public IReadOnlyList<ComicModel> GetComics()
    {
        return _comics;
    }

    public IReadOnlyList<ComicModel> GetSelectedComics()
    {
        return [.. _selectedComicItems.Select(x => x.Comic)];
    }

    public void CollapseOrExpandGroup(ComicGroupViewModel groupModel)
    {
        bool collapsed = !groupModel.Collapsed;
        groupModel.Collapsed = collapsed;

        ComicFilterModel.ExternalFilterModel? filter = _filterModel;
        if (filter is not null)
        {
            var collapsedGroups = filter.CollapsedGroups.ToHashSet();
            if (collapsed)
            {
                collapsedGroups.Add(groupModel.GroupName);
            }
            else
            {
                collapsedGroups.Remove(groupModel.GroupName);
            }

            filter.CollapsedGroups = collapsedGroups;
            filter.Modified = filter.SaveSortingAndGroupingSettings;
            UpdateLastFilter(filter);
        }

        UpdateCollapseExpandGroupButtonStates();
    }

    public void CollapseAllGroups()
    {
        HashSet<string> collapsedGroups = _filterModel?.CollapsedGroups.ToHashSet() ?? [];
        foreach (ComicGroupViewModel group in GroupedComicItems)
        {
            group.Collapsed = true;
            collapsedGroups.Add(group.GroupName);
        }

        ComicFilterModel.ExternalFilterModel? filter = _filterModel;
        if (filter is not null)
        {
            filter.CollapsedGroups = collapsedGroups;
            filter.Modified = filter.SaveSortingAndGroupingSettings;
            UpdateLastFilter(filter);
        }

        UpdateCollapseExpandGroupButtonStates();
    }

    public void ExpandAllGroups()
    {
        HashSet<string> collapsedGroups = _filterModel?.CollapsedGroups.ToHashSet() ?? [];
        foreach (ComicGroupViewModel group in GroupedComicItems)
        {
            group.Collapsed = false;
            collapsedGroups.Remove(group.GroupName);
        }

        ComicFilterModel.ExternalFilterModel? filter = _filterModel;
        if (filter is not null)
        {
            filter.CollapsedGroups = collapsedGroups;
            filter.Modified = filter.SaveSortingAndGroupingSettings;
            UpdateLastFilter(filter);
        }

        UpdateCollapseExpandGroupButtonStates();
    }

    //
    // Filters
    //

    private void SelectViewType(ComicFilterModel.ViewTypeEnum viewType)
    {
        _sharedDispatcher.Submit(() =>
        {
            ComicFilterModel.ExternalFilterModel filter = _filterModel ?? ComicFilterModel.ExternalFilterModel.FromDefault();
            if (filter.ViewType != viewType)
            {
                filter.ViewType = viewType;
                filter.Modified = filter.SaveViewSettings;
            }

            UpdateLastFilter(filter);
            ScheduleUpdateFilters(false);
        });
    }

    private void SelectSortOrGroup(Func<ComicFilterModel.ExternalFilterModel, bool> handler)
    {
        _sharedDispatcher.Submit(() =>
        {
            ComicFilterModel.ExternalFilterModel filter = _filterModel ?? ComicFilterModel.ExternalFilterModel.FromDefault();
            filter.Modified = handler(filter) && filter.SaveSortingAndGroupingSettings;
            UpdateLastFilter(filter);
            ScheduleUpdateFilters(false);
        });
    }

    private void UpdateLastFilter(ComicFilterModel.ExternalFilterModel filter)
    {
        ComicFilterModel.ExternalModel? filterSettings = _filterSettingsModel;
        if (filterSettings is not null)
        {
            filterSettings.LastFilter = filter.Clone();
            ComicFilterModel.Instance.UpdateModel(filterSettings);
        }

        UpdateUrl();
    }

    private void SelectFilterPreset(string? name)
    {
        name ??= "";
        _sharedDispatcher.Submit(() =>
        {
            ComicFilterModel.ExternalModel? filterSettings = _filterSettingsModel;
            if (filterSettings is null)
            {
                return;
            }

            ComicFilterModel.ExternalFilterModel? filter = MergeFilterFromDatabase(name, filterSettings);
            if (filter is null)
            {
                return;
            }

            filterSettings.LastFilter = filter;
            ComicFilterModel.Instance.UpdateModel(filterSettings);
            _filterModel = null;
            ScheduleUpdateFilters(false);
        });
    }

    private ComicFilterModel.ExternalFilterModel? MergeFilterFromDatabase(string name, ComicFilterModel.ExternalModel filterSettings)
    {
        ComicFilterModel.ExternalFilterModel? filter = filterSettings.Filters.Find(x => x.Name == name);
        if (filter is null)
        {
            return null;
        }

        filter = filter.Clone();
        ComicFilterModel.ExternalFilterModel? lastFilter = _filterModel;
        if (lastFilter is not null)
        {
            if (!filter.SaveViewSettings)
            {
                filter.ViewType = lastFilter.ViewType;
            }

            if (!filter.SaveSortingAndGroupingSettings)
            {
                filter.SortBy = lastFilter.SortBy;
                filter.ComicOrderMethod = lastFilter.ComicOrderMethod;
                filter.GroupBy = lastFilter.GroupBy;
                filter.GroupOrderMethod = lastFilter.GroupOrderMethod;
                filter.GroupSortingFunction = lastFilter.GroupSortingFunction;
                filter.GroupSortingProperty = lastFilter.GroupSortingProperty;
                filter.CollapsedGroups = lastFilter.CollapsedGroups;
            }
        }

        filter.Modified = false;
        return filter;
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
        _sharedDispatcher.SubmitAsync(async () =>
        {
            Interlocked.Exchange(ref _updateFilterSubmitted, 0);
            await UpdateFiltersNoLock(reloadFromDatabase);
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

        _sharedDispatcher.SubmitAsync(async () =>
        {
            Interlocked.Exchange(ref _updateComicSubmitted, 0);
            await DisplayComicsNoLock();
        });
    }

    //
    // Unsorted
    //

    private void OnComicSearchResult(IReadOnlyList<ComicModel> comics)
    {
        _comics = comics;
        ScheduleDisplayComics();
    }

    public static async Task BatchApplyOperation(ComicOperationType operationType, List<ComicItemViewModel> models)
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
                    await Task.WhenAll(items.Select(x => x.Comic.SetHidden(true)));
                }
                break;
            case ComicOperationType.Unhide:
                {
                    List<ComicItemViewModel> items = models.FindAll(x => x.IsHide);
                    await Task.WhenAll(items.Select(x => x.Comic.SetHidden(false)));
                }
                break;
            default:
                break;
        }
    }

    private void UpdateCommandBarButtonStates()
    {
        IEnumerable<ComicItemViewModel> selectedComicItems = _selectedComicItems.DistinctBy(x => x.Comic);
        bool allSelected = selectedComicItems.Count() == _comics.Count;
        bool favoriteEnabled = false;
        bool unfavoriteEnabled = false;
        bool completionStatusEnabled = false;
        bool hideEnabled = false;
        bool unhideEnabled = false;

        foreach (ComicItemViewModel item in selectedComicItems)
        {
            if (item.IsFavorite)
            {
                unfavoriteEnabled = true;
            }
            else
            {
                favoriteEnabled = true;
            }

            completionStatusEnabled = true;

            if (item.IsHide)
            {
                unhideEnabled = true;
            }
            else
            {
                hideEnabled = true;
            }
        }

        IsCommandBarSelectAllToggled = allSelected;
        IsCommandBarFavoriteEnabled = favoriteEnabled;
        IsCommandBarUnFavoriteEnabled = unfavoriteEnabled;
        IsCommandBarCompletionStatusEnabled = completionStatusEnabled;
        IsCommandBarHideEnabled = hideEnabled;
        IsCommandBarUnHideEnabled = unhideEnabled;
    }

    private void UpdateCollapseExpandGroupButtonStates()
    {
        if (!GroupingEnabledLiveData.HasValue)
        {
            return;
        }

        bool groupingEnabled = GroupingEnabledLiveData.Value;
        IsCollapseAllEnabled = groupingEnabled && GroupedComicItems.Count > 0 && GroupedComicItems.Any(x => !x.Collapsed);
        IsExpandAllEnabled = groupingEnabled && GroupedComicItems.Count > 0 && GroupedComicItems.Any(x => x.Collapsed);
    }

    private async Task UpdateFiltersNoLock(bool reloadFromDatabase)
    {
        Logger.I(TAG, "UpdateFiltersNoLock");

        // Validate and save filter settings
        List<ComicFilterModel.ExternalFilterModel> filters;
        ComicFilterModel.ExternalFilterModel? filter = _filterModel;
        {
            bool settingsChanged = false;
            ComicFilterModel.ExternalModel? filterSettings = _filterSettingsModel;
            if (reloadFromDatabase || filterSettings is null)
            {
                filterSettings = ComicFilterModel.Instance.GetModel() ?? new();
                _filterSettingsModel = filterSettings;
            }

            filters = filterSettings.Filters;
            if (filters == null || filters.Count == 0)
            {
                filters = [ComicFilterModel.ExternalFilterModel.FromDefault()];
                filterSettings.Filters = filters;
                settingsChanged = true;
            }

            filters.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

            ComicFilterModel.ExternalFilterModel? lastFilter = filterSettings.LastFilter;
            if (lastFilter is null)
            {
                lastFilter = filters[0].Clone();
                filterSettings.LastFilter = lastFilter;
                settingsChanged = true;
            }

            if (settingsChanged)
            {
                ComicFilterModel.Instance.UpdateModel(filterSettings);
            }

            if (filter is not null && reloadFromDatabase && !filter.Modified)
            {
                filter = MergeFilterFromDatabase(filter.Name, filterSettings);
                _filterModel = filter;
            }

            if (filter is null)
            {
                filter = lastFilter;
                _filterModel = lastFilter;
                UpdateUrl();
            }
        }

        // Update UI
        {
            var viewTypeDropDown = new DropDownButtonModel
            {
                Name = StringResourceProvider.Instance.ViewType,
                Items = _viewTypes.ConvertAll(x => new ToggleMenuFlyoutItemModel()
                {
                    Text = ViewTypeToDisplayName(x),
                    IsChecked = x == filter.ViewType,
                    Click = () =>
                    {
                        SelectViewType(x);
                    },
                }),
            };

            List<ComicPropertyModel> properties = await ComicPropertyModel.GetProperties();
            var sortByDropDown = new SubItemMenuFlyoutItemModel()
            {
                Text = StringResourceProvider.Instance.Sort,
                Items = CreateSortByMenuItems(properties, filter.SortBy, filter.ComicOrderMethod),
            };
            var groupByDropDown = new SubItemMenuFlyoutItemModel()
            {
                Text = StringResourceProvider.Instance.Group,
                Items = CreateGroupByMenuItems(properties, filter.GroupBy, filter.GroupOrderMethod, filter.GroupSortingFunction, filter.GroupSortingProperty),
            };
            var sortAndGroupDropDown = new DropDownButtonModel
            {
                Name = StringResourceProvider.Instance.Sort + " & " + StringResourceProvider.Instance.Group,
                Items = [sortByDropDown, groupByDropDown],
            };

            string lastFilterName = filter.Name ?? "";
            if (filter.Modified)
            {
                lastFilterName += " *";
            }

            var filterPresetDropDown = new DropDownButtonModel
            {
                Name = lastFilterName,
                Items = filters.ConvertAll(x => new SimpleMenuFlyoutItemModel()
                {
                    Text = x.Name,
                    Click = () => SelectFilterPreset(x.Name),
                }),
            };

            var uiModel = new FilterModel
            {
                ViewTypeDropDown = viewTypeDropDown,
                SortAndGroupDropDown = sortAndGroupDropDown,
                FilterPresetDropDown = filterPresetDropDown,
            };
            FilterLiveData.Emit(uiModel);
            ViewTypeLiveData.Emit(filter.ViewType);
        }

        // Update comics
        if (_searchEngine.Expression == filter.Expression)
        {
            ScheduleDisplayComics();
        }
        else
        {
            _searchEngine.Expression = filter.Expression;
            ScheduleUpdateComics();
        }
    }

    private async Task DisplayComicsNoLock()
    {
        Logger.I(TAG, "DisplayComicsNoLock");

        ComicItemViewModel ComicToViewModel(ComicModel comic, PlaylistModel.Builder playlist)
        {
            var item = new ComicItemViewModel(comic)
            {
                OnClick = model =>
                {
                    if (_isSelectMode)
                    {
                        return;
                    }

                    Route route = OpenComicHelper.GetComicRoute(comic, playlist: playlist);
                    OpenComicHelper.OpenComic(_actionHandler, route);
                },
                OnRequestContextFlyoutAsync = model =>
                {
                    List<ComicModel>? selectedComics = _isSelectMode ? _selectedComicItems.ConvertAll(x => x.Comic) : null;
                    return MenuFlyoutItemsCreator.CreateComicMenuItems(
                        _actionHandler,
                        comic,
                        playlist: playlist,
                        selectedComics: selectedComics,
                        canSelect: true);
                },
            };
            item.UpdateProgress(true);
            return item;
        }

        IReadOnlyList<ComicModel> comics = _comics;
        bool isEmpty = comics.Count == 0;
        List<ComicItemViewModel>? comicsUngrouped = null;
        List<ComicGroupViewModel>? comicsGrouped = null;

        if (isEmpty)
        {
            comicsUngrouped = [];
        }
        else
        {
            ComicFilterModel.ExternalFilterModel filter = _filterModel ?? ComicFilterModel.ExternalFilterModel.FromDefault();
            ComicPropertyModel sortBy = filter.SortBy;
            ComicPropertyModel? groupBy = filter.GroupBy;

            if (groupBy is not null)
            {
                List<ComicPropertyModel.GroupItem<ComicModel>> groups = groupBy.GroupComics(comics, x => x,
                    filter.GroupOrderMethod, filter.GroupSortingFunction, filter.GroupSortingProperty);
                var playlist = new PlaylistModel.Builder();
                comicsGrouped = [];
                foreach (ComicPropertyModel.GroupItem<ComicModel> group in groups)
                {
                    List<ComicModel> sorted = SortComicsByProerty(group.Items, sortBy, filter.ComicOrderMethod);
                    playlist.AddComics(sorted);
                    List<ComicItemViewModel> items = [.. sorted.Select(x => ComicToViewModel(x, playlist))];
                    var groupViewModel = new ComicGroupViewModel(group.Name, items, filter.CollapsedGroups.Contains(group.Name))
                    {
                        Description = group.Description,
                    };
                    comicsGrouped.Add(groupViewModel);
                }
            }
            else
            {
                List<ComicModel> sortedComics = SortComicsByProerty(comics, sortBy, filter.ComicOrderMethod);
                PlaylistModel.Builder playlist = new PlaylistModel.Builder().AddComics(sortedComics);
                comicsUngrouped = [.. sortedComics.Select(x => ComicToViewModel(x, playlist))];
            }
        }

        await MainThreadUtils.RunInMainThread(() =>
        {
            bool ComicComparer(ComicItemViewModel x, ComicItemViewModel y) => x.Comic.Id == y.Comic.Id;
            void ComicUpdater(ComicItemViewModel x, ComicItemViewModel y) => x.Update(y);

            if (comicsGrouped != null)
            {
                // Disable ME as it's causing a native crash in Microsoft.ui.xaml.dll.
                // This is not guaranteed a fix but so far it works fine. 
                // How to reproduce: Under group view (with 20+ groups), scroll to bottom (or close to bottom)
                // of the list. Then switch between different filter presets which share the same group names,
                // the crash should occur.
                // This problem can still be reproduced under lastest Windows SDK (1.8.250916003). The native
                // stack trace indicates that it relates to a MAUI collection component (likely GridView).
                DiffUtils.UpdateCollection(GroupedComicItems, comicsGrouped, (x, y) => x.GroupName == y.GroupName, (x, y) =>
                {
                    x.Collapsed = y.Collapsed;
                    x.Description = y.Description;
                    x.UpdateItems(y.Items, ComicComparer, ComicUpdater);
                }, disableME: true);

                GroupingEnabledLiveData.Emit(true);
                LibraryEmptyVisible = GroupedComicItems.Count == 0;
            }
            else if (comicsUngrouped != null)
            {
                DiffUtils.UpdateCollection(UngroupedComicItems, comicsUngrouped, ComicComparer, ComicUpdater);
                GroupingEnabledLiveData.Emit(false);
                LibraryEmptyVisible = UngroupedComicItems.Count == 0;
            }
            else
            {
                Logger.F(TAG, "Shouldn't reach");
            }

            UpdateCollapseExpandGroupButtonStates();
            UpdateCommandBarButtonStates();
        });
    }

    private void UpdateUrl()
    {
        ComicFilterModel.ExternalFilterModel? filter = _filterModel;
        if (filter is null)
        {
            return;
        }

        ComicFilterModel.FilterModel jsonModel = filter.To();
        string json = JsonSerializer.Serialize(jsonModel);
        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_HOME)
            .WithParam(RouterConstants.ARG_FILTER_JSON, json);
        UrlLiveData.Emit(route.Url);
    }

    private static List<ComicModel> SortComicsByProerty(IReadOnlyList<ComicModel> comics,
        ComicPropertyModel property, ComicFilterModel.OrderMethodEnum orderMethod)
    {
        return property.SortComics(comics, x => x, orderMethod);
    }

    private List<BaseMenuFlyoutItemModel> CreateSortByMenuItems(List<ComicPropertyModel> properties,
        ComicPropertyModel? selectedProperty, ComicFilterModel.OrderMethodEnum selectedOrderMethod)
    {
        List<BaseMenuFlyoutItemModel> items = [];

        items.AddRange(CreateSortByPropertyMenuItems(properties, selectedProperty, p =>
        {
            SelectSortOrGroup(filter =>
            {
                if (!p.Equals(filter.SortBy))
                {
                    filter.SortBy = p;
                    return true;
                }

                return false;
            });
        }));

        if (selectedProperty != null)
        {
            items.Add(new SeparatorMenuFlyoutItemModel());
            items.AddRange(CreateOrderMethodMenuItems(selectedOrderMethod, orderMethod =>
            {
                SelectSortOrGroup(filter =>
                {
                    if (orderMethod != filter.ComicOrderMethod)
                    {
                        filter.ComicOrderMethod = orderMethod;
                        return true;
                    }

                    return false;
                });
            }));
        }

        return items;
    }

    private List<BaseMenuFlyoutItemModel> CreateGroupByMenuItems(List<ComicPropertyModel> properties,
        ComicPropertyModel? selectedProperty, ComicFilterModel.OrderMethodEnum selectedOrderMethod,
        ComicFilterModel.FunctionTypeEnum sortingFunction, ComicPropertyModel? sortingProperty)
    {
        List<BaseMenuFlyoutItemModel> items = [];

        items.Add(new ToggleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.None,
            IsChecked = selectedProperty is null,
            Click = () =>
            {
                SelectSortOrGroup(filter =>
                {
                    if (filter.GroupBy is not null)
                    {
                        filter.GroupBy = null;
                        return true;
                    }

                    return false;
                });
            }
        });

        items.AddRange(CreateSortByPropertyMenuItems(properties, selectedProperty, p =>
        {
            SelectSortOrGroup(filter =>
            {
                if (!p.Equals(filter.GroupBy))
                {
                    filter.GroupBy = p;
                    return true;
                }

                return false;
            });
        }));

        if (selectedProperty != null)
        {
            items.Add(new SeparatorMenuFlyoutItemModel());

            items.AddRange(CreateOrderMethodMenuItems(selectedOrderMethod, orderMethod =>
            {
                SelectSortOrGroup(filter =>
                {
                    if (orderMethod != filter.GroupOrderMethod)
                    {
                        filter.GroupOrderMethod = orderMethod;
                        return true;
                    }

                    return false;
                });
            }));

            items.Add(new SubItemMenuFlyoutItemModel()
            {
                Text = StringResourceProvider.Instance.SortingFunction,
                Items = CreateSortingFunctionMenuItems(properties, sortingFunction, sortingProperty, (f, p) =>
                {
                    SelectSortOrGroup(filter =>
                    {
                        bool modified = false;

                        if (filter.GroupSortingFunction != f)
                        {
                            modified = true;
                            filter.GroupSortingFunction = f;
                        }

                        if (p is not null && !p.Equals(filter.GroupSortingProperty))
                        {
                            modified = true;
                            filter.GroupSortingProperty = p;
                        }

                        return modified;
                    });
                }),
            });
        }

        return items;
    }

    private static List<BaseMenuFlyoutItemModel> CreateOrderMethodMenuItems(ComicFilterModel.OrderMethodEnum selectedMethod, Action<ComicFilterModel.OrderMethodEnum> clickHandler)
    {
        string GetOrderMethodDisplayName(ComicFilterModel.OrderMethodEnum method)
        {
            return method switch
            {
                ComicFilterModel.OrderMethodEnum.Ascending => StringResourceProvider.Instance.Ascending,
                ComicFilterModel.OrderMethodEnum.Descending => StringResourceProvider.Instance.Descending,
                ComicFilterModel.OrderMethodEnum.Shuffle => StringResourceProvider.Instance.Shuffle,
                ComicFilterModel.OrderMethodEnum.ShuffleStable => StringResourceProvider.Instance.ShuffleStable,
                _ => "???"
            };
        }

        List<ComicFilterModel.OrderMethodEnum> orderMethods = [
            ComicFilterModel.OrderMethodEnum.Ascending,
            ComicFilterModel.OrderMethodEnum.Descending,
            ComicFilterModel.OrderMethodEnum.Shuffle,
            ComicFilterModel.OrderMethodEnum.ShuffleStable,
        ];

        List<BaseMenuFlyoutItemModel> items = [];
        foreach (ComicFilterModel.OrderMethodEnum orderMethod in orderMethods)
        {
            items.Add(new ToggleMenuFlyoutItemModel()
            {
                Text = GetOrderMethodDisplayName(orderMethod),
                IsChecked = orderMethod == selectedMethod,
                Click = () =>
                {
                    clickHandler(orderMethod);
                },
            });
        }

        return items;
    }

    private static List<BaseMenuFlyoutItemModel> CreateSortingFunctionMenuItems(List<ComicPropertyModel> properties,
        ComicFilterModel.FunctionTypeEnum sortingFunction, ComicPropertyModel? sortingProperty,
        Action<ComicFilterModel.FunctionTypeEnum, ComicPropertyModel?> clickHandler)
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

        List<BaseMenuFlyoutItemModel> items = [];
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
            items.Add(new ToggleMenuFlyoutItemModel()
            {
                Text = GetFunctionDisplayName(function),
                IsChecked = function == sortingFunction,
                Click = () =>
                {
                    clickHandler(function, null);
                }
            });
        }

        foreach (ComicFilterModel.FunctionTypeEnum function in propertyFunctions)
        {
            IEnumerable<BaseMenuFlyoutItemModel> subItems = CreateSortByPropertyMenuItems(properties, function == sortingFunction ? sortingProperty : null, p =>
            {
                clickHandler(function, p);
            });
            items.Add(new SubItemMenuFlyoutItemModel()
            {
                Text = GetFunctionDisplayName(function),
                Items = subItems,
            });
        }

        return items;
    }

    private static IEnumerable<BaseMenuFlyoutItemModel> CreateSortByPropertyMenuItems(List<ComicPropertyModel> properties,
        ComicPropertyModel? selectedProperty, Action<ComicPropertyModel> clickHandler)
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
            if (kvp.Key == string.Empty)
            {
                plainProperties = kvp.Value;
                continue;
            }

            propertyGroupList.Add(kvp);
            kvp.Value.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName));
        }

        List<Tuple<string, BaseMenuFlyoutItemModel>> items = [];
        if (plainProperties is not null)
        {
            foreach (ComicPropertyModel p in plainProperties)
            {
                items.Add(new(p.DisplayName, new ToggleMenuFlyoutItemModel()
                {
                    Text = p.DisplayName,
                    IsChecked = p.Equals(selectedProperty),
                    Click = () =>
                    {
                        clickHandler(p);
                    }
                }));
            }
        }

        foreach (KeyValuePair<string, List<ComicPropertyModel>> kvp in propertyGroupList)
        {
            List<BaseMenuFlyoutItemModel> subItems = [];
            foreach (ComicPropertyModel p in kvp.Value)
            {
                subItems.Add(new ToggleMenuFlyoutItemModel()
                {
                    Text = p.DisplayName,
                    IsChecked = p.Equals(selectedProperty),
                    Click = () =>
                    {
                        clickHandler(p);
                    }
                });
            }

            items.Add(new(kvp.Key, new SubItemMenuFlyoutItemModel()
            {
                Text = kvp.Key,
                Items = subItems,
            }));
        }

        items.Sort((a, b) => string.Compare(a.Item1, b.Item1, StringComparison.InvariantCultureIgnoreCase));
        return items.Select(x => x.Item2);
    }

    private static string ViewTypeToDisplayName(ComicFilterModel.ViewTypeEnum viewType)
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
        public DropDownButtonModel ViewTypeDropDown { get; set; } = new();
        public DropDownButtonModel SortAndGroupDropDown { get; set; } = new();
        public DropDownButtonModel FilterPresetDropDown { get; set; } = new();
    }

    public class DropDownButtonModel
    {
        public string Name { get; set; } = "";
        public IEnumerable<BaseMenuFlyoutItemModel> Items { get; set; } = [];
    }
}
