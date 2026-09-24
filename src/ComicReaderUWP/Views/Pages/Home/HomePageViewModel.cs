// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Localization;
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
using ComicReaderUWP.UserControls.ComicSelection;
using ComicReaderUWP.ViewModels;

namespace ComicReaderUWP.Views.Pages.Home;

internal partial class HomePageViewModel : INotifyPropertyChanged
{
    private const string TAG = nameof(HomePageViewModel);

    public event PropertyChangedEventHandler? PropertyChanged;

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

    public readonly MutableLiveData<string> UrlLiveData = new();
    public readonly MutableLiveData<FilterModel> FilterLiveData = new();
    public readonly MutableLiveData<bool> GroupingEnabledLiveData = new();
    public readonly MutableLiveData<ComicFilterModel.ViewTypeEnum> ViewTypeLiveData = new();

    public List<ComicItemViewModel> UngroupedComicItems { get; private set; } = [];
    public List<ComicGroupViewModel> GroupedComicItems { get; private set; } = [];
    public ComicSelectionViewModel ComicSelection { get; }

    private ActionHandler _actionHandler = ActionHandler.Dummy;
    private readonly ComicSearchEngine _searchEngine = new();
    private ComicFilterModel.ExternalModel? _filterSettingsModel;
    private ComicFilterModel.ExternalFilterModel? _filterModel;
    private IReadOnlyList<ComicModel> _comics = [];
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

    public HomePageViewModel()
    {
        ComicSelection = new(() => _comics.Count);
    }

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

    public IReadOnlyList<ComicModel> GetComics()
    {
        return _comics;
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
            filter.Modified |= filter.SaveSortingAndGroupingSettings;
            UpdateLastFilter(filter);
        }
    }

    public void CollapseAllGroups()
    {
        ComicFilterModel.ExternalFilterModel? filter = _filterModel;
        HashSet<string> collapsedGroups = filter?.CollapsedGroups.ToHashSet() ?? [];
        bool changed = false;
        foreach (ComicGroupViewModel group in GroupedComicItems)
        {
            changed |= collapsedGroups.Add(group.GroupName);
        }

        if (!changed)
        {
            return;
        }

        if (filter is not null)
        {
            filter.CollapsedGroups = collapsedGroups;
            filter.Modified |= filter.SaveSortingAndGroupingSettings;
            UpdateLastFilter(filter);
        }

        ScheduleDisplayComics();
    }

    public void ExpandAllGroups()
    {
        ComicFilterModel.ExternalFilterModel? filter = _filterModel;
        HashSet<string> collapsedGroups = filter?.CollapsedGroups.ToHashSet() ?? [];
        bool changed = false;
        foreach (ComicGroupViewModel group in GroupedComicItems)
        {
            changed |= collapsedGroups.Remove(group.GroupName);
        }

        if (!changed)
        {
            return;
        }

        if (filter is not null)
        {
            filter.CollapsedGroups = collapsedGroups;
            filter.Modified |= filter.SaveSortingAndGroupingSettings;
            UpdateLastFilter(filter);
        }

        ScheduleDisplayComics();
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
                filter.Modified |= filter.SaveViewSettings;
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
            bool changed = handler(filter);
            filter.Modified |= changed && filter.SaveSortingAndGroupingSettings;
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
                filter.MergeSingleItemGroups = lastFilter.MergeSingleItemGroups;
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
                Items = CreateGroupByMenuItems(properties, filter),
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
        if (_searchEngine.Expression == filter.Expression && _searchEngine.IncludeHidden == filter.IncludeHiddenComics)
        {
            ScheduleDisplayComics();
        }
        else
        {
            _searchEngine.Expression = filter.Expression;
            _searchEngine.IncludeHidden = filter.IncludeHiddenComics;
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
                    if (ComicSelection.IsSelectMode)
                    {
                        return;
                    }

                    Route route = OpenComicHelper.GetComicRoute(comic, playlist: playlist);
                    OpenComicHelper.OpenComic(_actionHandler, route);
                },
                OnRequestContextFlyoutAsync = model =>
                {
                    IReadOnlyList<ComicModel>? selectedComics = ComicSelection.IsSelectMode ? ComicSelection.GetSelectedComics() : null;
                    return MenuFlyoutItemsCreator.CreateComicMenuItems(
                        _actionHandler,
                        comic,
                        playlist: playlist,
                        selectedItems: selectedComics,
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
                    filter.GroupOrderMethod,
                    filter.GroupSortingFunction,
                    filter.GroupSortingProperty,
                    filter.MergeSingleItemGroups);
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
            if (comicsGrouped != null)
            {
                GroupedComicItems = comicsGrouped;
                UngroupedComicItems = [];
                GroupingEnabledLiveData.Emit(true);
                LibraryEmptyVisible = GroupedComicItems.Count == 0;
            }
            else if (comicsUngrouped != null)
            {
                UngroupedComicItems = comicsUngrouped;
                GroupedComicItems = [];
                GroupingEnabledLiveData.Emit(false);
                LibraryEmptyVisible = UngroupedComicItems.Count == 0;
            }
            else
            {
                Logger.F(TAG, "Shouldn't reach");
            }
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

    private List<BaseMenuFlyoutItemModel> CreateGroupByMenuItems(List<ComicPropertyModel> properties, ComicFilterModel.ExternalFilterModel filter)
    {
        ComicPropertyModel? selectedProperty = filter.GroupBy;

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

            items.AddRange(CreateOrderMethodMenuItems(filter.GroupOrderMethod, orderMethod =>
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
                Items = CreateSortingFunctionMenuItems(properties, filter.GroupSortingFunction, filter.GroupSortingProperty, (f, p) =>
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

            items.Add(new SeparatorMenuFlyoutItemModel());

            items.Add(new ToggleMenuFlyoutItemModel()
            {
                Text = StringResourceProvider.Instance.MergeSingleItemGroups,
                IsChecked = filter.MergeSingleItemGroups,
                Click = () =>
                {
                    SelectSortOrGroup(filter =>
                    {
                        filter.MergeSingleItemGroups = !filter.MergeSingleItemGroups;
                        return true;
                    });
                },
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
