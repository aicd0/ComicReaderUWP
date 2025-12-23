// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ComicReader.Common.Actions;
using ComicReader.Common.Actions.Providers;
using ComicReader.Common.Constants;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.Helpers.Navigation;
using ComicReader.Helpers.Search;
using ComicReader.SDK.Common.Algorithm;
using ComicReader.SDK.Common.Lifecycle;
using ComicReader.SDK.Common.Threading;
using ComicReader.SDK.Common.Utils;
using ComicReader.SDK.Database.KV;
using ComicReader.ViewModels;

namespace ComicReader.Views.Pages.SidePane.FilterPresets;

internal partial class FilterPresetsPageViewModel : INotifyPropertyChanged
{
    private const int SEARCH_DELAY = 200;

    public event PropertyChangedEventHandler? PropertyChanged;

    public readonly MutableLiveData<DropDownButtonModel> FilterPresetDropDownLiveData = new();

    public ObservableCollection<SimpleTreeViewNodeModel> DataSource { get; set; } = [];

    public bool _selectionMode = false;
    public bool SelectionMode
    {
        get => _selectionMode;
        set
        {
            _selectionMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectionMode)));
        }
    }

    private ActionHandler _actionHandler = ActionHandler.Dummy;
    private readonly ComicSearchEngine _searchEngine = new();
    private ComicFilterModel.ExternalFilterModel? _selectedFilter;
    private bool _searchSubmitted = false;

    private readonly ITaskDispatcher _sharedDispatcher = TaskDispatcher.Factory.NewQueue("FilterPresetsPageQueue");
    private int _updateComicSubmitted = 0;

    public void Initialize(ActionHandler actionHandler)
    {
        _actionHandler = actionHandler;
        _searchEngine.SetResultCallback(OnComicSearchResult);
    }

    public void UpdateComics()
    {
        ScheduleUpdateComics();
    }

    public ComicModel? GetRandomComic()
    {
        List<ComicModel> comics = [];
        foreach (SimpleTreeViewNodeModel node in DataSource)
        {
            foreach (ComicModel comic in node.CollectDataContext<ComicModel>())
            {
                comics.Add(comic);
            }
        }

        if (comics.Count == 0)
        {
            return null;
        }

        int index = Random.Shared.Next(comics.Count);
        return comics[index];
    }

    public void SetSearchText(string searchText)
    {
        searchText = searchText.Trim();
        if (searchText == _searchEngine.SearchText)
        {
            return;
        }

        _searchEngine.SearchText = searchText;
        if (_searchSubmitted)
        {
            return;
        }

        _searchSubmitted = true;
        CoroutineUtils.Start(async () =>
        {
            await Task.Delay(SEARCH_DELAY);
            _searchSubmitted = false;
            _searchEngine.Update();
        });
    }

    public void ExpandAllGroups()
    {
        foreach (SimpleTreeViewNodeModel node in DataSource)
        {
            node.ExpandAll();
        }
    }

    public void CollapseAllGroups()
    {
        foreach (SimpleTreeViewNodeModel node in DataSource)
        {
            node.CollapseAll();
        }
    }

    private void SetFilter(ComicFilterModel.ExternalFilterModel filter)
    {
        if (_selectedFilter is not null && _selectedFilter.Name == filter.Name)
        {
            return;
        }

        _selectedFilter = filter;
        ScheduleUpdateComics();
    }

    private void ScheduleUpdateComics()
    {
        if (Interlocked.CompareExchange(ref _updateComicSubmitted, 1, 0) == 1)
        {
            return;
        }

        _sharedDispatcher.Submit("ScheduleUpdateComics", () =>
        {
            Interlocked.Exchange(ref _updateComicSubmitted, 0);
            UpdateComicsNoLock();
        });
    }

    private void UpdateComicsNoLock()
    {
        ComicFilterModel.ExternalModel filterModel = ComicFilterModel.Instance.GetModel() ?? new();
        List<ComicFilterModel.ExternalFilterModel> filters = filterModel.Filters;
        if (filters == null || filters.Count == 0)
        {
            filters = [ComicFilterModel.ExternalFilterModel.FromDefault()];
            filterModel.Filters = filters;
        }

        filters.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        ComicFilterModel.ExternalFilterModel? selectedFilter = null;
        if (_selectedFilter is not null)
        {
            selectedFilter = filters.Find(x => x.Name == _selectedFilter.Name);
        }

        if (selectedFilter is null)
        {
            string? lastFilterName = KVStore.Default.GetCollection(DatabaseEntry.KV_LIB_APP).GetValue<string>(DatabaseEntry.KV_KEY_APP_SIDE_PANE_LAST_FILTER_PRESET);
            if (!string.IsNullOrEmpty(lastFilterName))
            {
                selectedFilter = filters.Find(x => x.Name == lastFilterName);
            }
        }

        selectedFilter ??= filters[0];
        _selectedFilter = selectedFilter;
        KVStore.Default.GetCollection(DatabaseEntry.KV_LIB_APP).Set(DatabaseEntry.KV_KEY_APP_SIDE_PANE_LAST_FILTER_PRESET, selectedFilter.Name);

        DropDownButtonModel filterPresetDropdown = new()
        {
            Name = selectedFilter.Name,
            Items = filters.ConvertAll(x => new ToggleMenuFlyoutItemModel(x.Name)
            {
                IsChecked = x.Name == selectedFilter.Name,
                Click = () =>
                {
                    SetFilter(x);
                }
            })
        };
        FilterPresetDropDownLiveData.Emit(filterPresetDropdown);

        _searchEngine.Expression = selectedFilter.Expression;
        _searchEngine.Update();
    }

    private void OnComicSearchResult(IReadOnlyList<ComicModel> items)
    {
        _sharedDispatcher.Submit("OnComicSearchResult", () =>
        {
            ComicFilterModel.ExternalFilterModel filter = _selectedFilter
                ?? ComicFilterModel.ExternalFilterModel.FromDefault();
            ComicPropertyModel sortBy = filter.SortBy;
            ComicPropertyModel? groupBy = filter.GroupBy;

            List<SimpleTreeViewNodeModel> dataSource = [];
            SimpleTreeViewNodeModel ComicToNode(ComicModel comic)
            {
                return new()
                {
                    DataContext = comic,
                    Glyph = "\uE8B9",
                    Title = comic.Title,
                    CanExpand = false,
                    Clicked = () =>
                    {
                        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                            .WithParam(RouterConstants.ARG_COMIC_ID, comic.Id.ToString());
                        ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                            .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
                            .AddParameter(OpenTabProvider.PARAM_NEW_TAB, "0")
                            .Build();
                        _actionHandler.Handle(actionModel);
                    },
                    RequestContextMenuItemsAsync = (primary, selection) =>
                    {
                        IEnumerable<ComicModel> selectedComics = selection.Where(x => x.DataContext is ComicModel).Select(x => (ComicModel)x.DataContext!);
                        return MenuFlyoutItemsCreator.CreateComicMenuItems(comic, _actionHandler, selectedComics, canSelect: !SelectionMode);
                    },
                };
            }

            if (groupBy != null)
            {
                List<ComicPropertyModel.GroupItem<ComicModel>> groupItems = groupBy.GroupComics(items, x => x,
                    filter.GroupOrderMethod, filter.GroupSortingFunction, filter.GroupSortingProperty);
                foreach (ComicPropertyModel.GroupItem<ComicModel> item in groupItems)
                {
                    SimpleTreeViewNodeModel groupNode = new()
                    {
                        Title = item.Name,
                        CanExpand = true,
                        IsExpanded = false,
                        Description = item.Description,
                        RequestContextMenuItemsAsync = (primary, selection) =>
                        {
                            return CreateGroupMenuItems(primary);
                        },
                    };

                    List<SimpleTreeViewNodeModel> nodeChildren = [];
                    List<ComicModel> sorted = sortBy.SortComics(item.Items, x => x, filter.ComicOrderMethod);
                    foreach (ComicModel comic in sorted)
                    {
                        groupNode.Children.Add(ComicToNode(comic));
                    }

                    dataSource.Add(groupNode);
                }
            }
            else
            {
                List<ComicModel> sorted = sortBy.SortComics(items, x => x, filter.ComicOrderMethod);
                foreach (ComicModel comic in sorted)
                {
                    dataSource.Add(ComicToNode(comic));
                }
            }

            CoroutineUtils.RunInMainThread(() =>
            {
                void UpdateItem(SimpleTreeViewNodeModel from, SimpleTreeViewNodeModel to)
                {
                    from.Glyph = to.Glyph;
                    from.Description = to.Description;
                    from.CanExpand = to.CanExpand;
                    from.Clicked = to.Clicked;
                    from.RequestContextMenuItemsAsync = to.RequestContextMenuItemsAsync;
                    DiffUtils.UpdateCollection(from.Children, to.Children, (a, b) => a.Title == b.Title, UpdateItem);
                }

                DiffUtils.UpdateCollection(DataSource, dataSource, (x, y) => x.Title == y.Title, UpdateItem);
            });
        });
    }

    private Task<List<BaseMenuFlyoutItemModel>> CreateGroupMenuItems(SimpleTreeViewNodeModel node)
    {
        List<ComicModel> comics = [.. node.CollectDataContext<ComicModel>()];
        ComicModel? randomComic = comics.Count > 0 ? comics[Random.Shared.Next(comics.Count)] : null;
        return MenuFlyoutItemsCreator.CreateComicGroupMenuItems(_actionHandler, randomComic,
            node.ExpandAll, node.CollapseAll);
    }

    public class DropDownButtonModel
    {
        public string Name { get; set; } = string.Empty;
        public IEnumerable<BaseMenuFlyoutItemModel> Items { get; set; } = [];
    }
}
