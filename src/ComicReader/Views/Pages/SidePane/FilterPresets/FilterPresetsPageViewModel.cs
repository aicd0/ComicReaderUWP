// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

using ComicReader.Common.Actions;
using ComicReader.Common.Actions.Providers;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.Helpers.Navigation;
using ComicReader.Helpers.Search;
using ComicReader.SDK.Common.Algorithm;
using ComicReader.SDK.Common.Lifecycle;
using ComicReader.SDK.Common.Threading;
using ComicReader.ViewModels;

namespace ComicReader.Views.Pages.SidePane.FilterPresets;

internal class FilterPresetsPageViewModel
{
    public readonly MutableLiveData<DropDownButtonModel> FilterPresetDropDownLiveData = new();

    public ObservableCollection<TagNodeViewModel> DataSource { get; set; } = [];

    private ActionHandler _actionHandler = ActionHandler.Dummy;
    private readonly ComicSearchEngine _searchEngine = new();
    private ComicFilterModel.ExternalFilterModel? _selectedFilter;

    private readonly ITaskDispatcher _sharedDispatcher = TaskDispatcher.DefaultQueue;
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

        selectedFilter ??= filters[0];
        _selectedFilter = selectedFilter;

        DropDownButtonModel filterPresetDropdown = new()
        {
            Name = selectedFilter.Name,
            Items = filters.ConvertAll(x => new MenuFlyoutToggleItemViewModel(x.Name)
            {
                IsChecked = x.Name == selectedFilter.Name,
                OnClick = () =>
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

            List<TagNodeViewModel> dataSource = [];
            TagNodeViewModel ComicToNode(ComicModel comic)
            {
                return new()
                {
                    Glyph = "\uE8B9",
                    Title = comic.Title,
                    CanExpand = false,
                    OnClick = () =>
                    {
                        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                            .WithParam(RouterConstants.ARG_COMIC_ID, comic.Id.ToString());
                        ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                            .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
                            .AddParameter(OpenTabProvider.PARAM_NEW_TAB, "0")
                            .Build();
                        _actionHandler.Handle(actionModel);
                    },
                    OnRequestContextFlyoutAsync = () =>
                    {
                        return MenuFlyoutItemsCreator.CreateMenuItems(comic, _actionHandler);
                    },
                };
            }

            if (groupBy != null)
            {
                List<ComicPropertyModel.GroupItem<ComicModel>> groupItems = groupBy.GroupComics(items, x => x,
                    filter.GroupOrderMethod, filter.GroupSortingFunction, filter.GroupSortingProperty);
                foreach (ComicPropertyModel.GroupItem<ComicModel> item in groupItems)
                {
                    TagNodeViewModel groupNode = new()
                    {
                        Title = item.Name,
                        CanExpand = true,
                        Expanded = false,
                        Description = item.Description,
                    };

                    List<TagNodeViewModel> nodeChildren = [];
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

            MainThreadUtils.RunInMainThread(() =>
            {
                void UpdateItem(TagNodeViewModel from, TagNodeViewModel to)
                {
                    from.Glyph = to.Glyph;
                    from.Description = to.Description;
                    from.CanExpand = to.CanExpand;
                    from.OnClick = to.OnClick;
                    from.OnRequestContextFlyoutAsync = to.OnRequestContextFlyoutAsync;
                    DiffUtils.UpdateCollection(from.Children, to.Children, (a, b) => a.Title == b.Title, UpdateItem);
                }

                DiffUtils.UpdateCollection(DataSource, dataSource, (x, y) => x.Title == y.Title, UpdateItem);
            });
        });
    }

    public class DropDownButtonModel
    {
        public string Name { get; set; } = string.Empty;
        public IEnumerable<BaseMenuFlyoutItemViewModel> Items { get; set; } = [];
    }
}
