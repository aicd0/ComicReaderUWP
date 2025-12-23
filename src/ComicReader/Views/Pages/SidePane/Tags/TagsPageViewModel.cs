// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Actions;
using ComicReader.Common.Utils;
using ComicReader.Data.Models.Comic;
using ComicReader.Data.Models.TagInfo;
using ComicReader.Data.Tables;
using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.Algorithm;
using ComicReader.SDK.Common.Lifecycle;
using ComicReader.SDK.Common.Utils;
using ComicReader.SDK.Database.SqlHelpers;
using ComicReader.ViewModels;

namespace ComicReader.Views.Pages.SidePane.Tags;

internal partial class TagsPageViewModel : INotifyPropertyChanged
{
    private const int SEARCH_DELAY = 200;

    public event PropertyChangedEventHandler? PropertyChanged;

    public readonly MutableLiveData<Route> OpenInCurrentTabLiveData = new();
    public readonly MutableLiveData<Route> OpenInNewTabLiveData = new();
    public readonly MutableLiveData<string> EditTagCategoryLiveData = new();
    public readonly MutableLiveData<KeyValuePair<string, string>> EditTagLiveData = new();

    public ObservableCollection<SimpleTreeViewNodeModel> DataSource { get; set; } = [];

    private bool _noTagsVisible = false;
    public bool NoTagsVisible
    {
        get => _noTagsVisible;
        set
        {
            if (_noTagsVisible != value)
            {
                _noTagsVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NoTagsVisible)));
            }
        }
    }

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
    private bool _updatingTags = false;
    private bool _updatingTagsInvalidated = false;
    private string _searchText = string.Empty;
    private bool _searchSubmitted = false;

    public void Initialize(ActionHandler actionHandler)
    {
        _actionHandler = actionHandler;
    }

    public void UpdateTags()
    {
        CoroutineUtils.Start(ScheduleUpdateTags);
    }

    public void SetSearchText(string searchText)
    {
        searchText = searchText.Trim();
        if (searchText == _searchText)
        {
            return;
        }

        _searchText = searchText;
        if (_searchSubmitted)
        {
            return;
        }

        _searchSubmitted = true;
        CoroutineUtils.Start(async () =>
        {
            await Task.Delay(SEARCH_DELAY);
            _searchSubmitted = false;
            await ScheduleUpdateTags();
        });
    }

    private async Task ScheduleUpdateTags()
    {
        if (_updatingTags)
        {
            _updatingTagsInvalidated = true;
            return;
        }

        _updatingTags = true;
        try
        {
            do
            {
                _updatingTagsInvalidated = false;
                await UpdateTagsInternal();
            }
            while (_updatingTagsInvalidated);
        }
        finally
        {
            _updatingTags = false;
        }
    }

    private async Task UpdateTagsInternal()
    {
        List<SimpleTreeViewNodeModel> dataSource = await GenerateNodeTree();

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
        NoTagsVisible = dataSource.Count == 0;
    }

    private async Task<List<SimpleTreeViewNodeModel>> GenerateNodeTree()
    {
        Dictionary<long, TagCateogryEntry> tagCategoryMapper = [];
        await ComicData.Enqueue("UpdateTags", () =>
        {
            {
                var command = SelectCommand.Create(TagCategoryTable.Instance);
                IReaderToken<long> idToken = command.PutQueryInt64(TagCategoryTable.ColumnId);
                IReaderToken<string> nameToken = command.PutQueryString(TagCategoryTable.ColumnName);
                IReaderToken<long> comicIdToken = command.PutQueryInt64(TagCategoryTable.ColumnComicId);
                using SelectCommand.IReader reader = command.Execute();
                while (reader.Read())
                {
                    long id = idToken.GetValue();
                    string name = nameToken.GetValue();
                    long comicId = comicIdToken.GetValue();
                    tagCategoryMapper[id] = new(name, comicId);
                }
            }

            {
                var command = SelectCommand.Create(TagTable.Instance);
                IReaderToken<string> contentToken = command.PutQueryString(TagTable.ColumnContent);
                IReaderToken<long> categoryIdToken = command.PutQueryInt64(TagTable.ColumnTagCategoryId);
                using SelectCommand.IReader reader = command.Execute();
                while (reader.Read())
                {
                    string content = contentToken.GetValue();
                    long categoryId = categoryIdToken.GetValue();
                    if (tagCategoryMapper.TryGetValue(categoryId, out TagCateogryEntry? tagCategoryModel))
                    {
                        tagCategoryModel.Tags.Add(content);
                    }
                }
            }

            return true;
        });

        Dictionary<string, Dictionary<string, TagEntry>> tagCategoryMap = [];
        TagEntry PutTag(string tagCategory, string tag)
        {
            if (!tagCategoryMap.TryGetValue(tagCategory, out Dictionary<string, TagEntry>? tags))
            {
                tags = [];
                tagCategoryMap[tagCategory] = tags;
            }

            if (!tags.TryGetValue(tag, out TagEntry? tagModel))
            {
                tagModel = new();
                tags[tag] = tagModel;
            }

            return tagModel;
        }

        HashSet<long> requestingComicIds = [];
        foreach (TagCateogryEntry tagCategoryModel in tagCategoryMapper.Values)
        {
            foreach (string tag in tagCategoryModel.Tags)
            {
                TagEntry tagModel = PutTag(tagCategoryModel.Name, tag);
                tagModel.ComicIds.Add(tagCategoryModel.ComicId);
                requestingComicIds.Add(tagCategoryModel.ComicId);
            }
        }

        Dictionary<long, ComicModel> comicMap = [];
        {
            List<ComicModel> requestedComics = await ComicModel.BatchFromId("UpdateTags", requestingComicIds);
            foreach (ComicModel comic in requestedComics)
            {
                if (comic.Hidden)
                {
                    continue;
                }

                comicMap[comic.Id] = comic;
            }
        }

        string[] keywords = string.IsNullOrEmpty(_searchText) ? [] : _searchText.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        bool MatchSearchText(string text)
        {
            return keywords.Length == 0 || StringUtils.FastMatch(keywords, text.ToLowerInvariant()) > 0;
        }

        List<SimpleTreeViewNodeModel> dataSource = [];
        List<string> tagCategories = [.. tagCategoryMap.Keys];
        tagCategories.Sort();
        foreach (string tagCategory in tagCategories)
        {
            Dictionary<string, TagEntry> tagMap = tagCategoryMap[tagCategory];
            List<string> tags = [.. tagMap.Keys];
            tags.Sort();

            TagCateogryModel tagCategoryModel = new(tagCategory);
            SimpleTreeViewNodeModel tagCategoryNode = new()
            {
                DataContext = tagCategoryModel,
                Title = tagCategory,
                CanExpand = true,
                IsExpanded = true,
                RequestContextMenuItemsAsync = CreateTagCategoryMenuItems,
            };

            foreach (string tag in tags)
            {
                TagEntry tagEntry = tagMap[tag];
                bool tagMatched = MatchSearchText(tag);

                TagModel tagModel = new(tagCategory, tag);
                SimpleTreeViewNodeModel tagNode = new()
                {
                    DataContext = tagModel,
                    Glyph = "\uE8EC",
                    Title = tag,
                    CanExpand = true,
                    IsExpanded = false,
                    RequestContextMenuItemsAsync = CreateTagMenuItems,
                };

                List<SimpleTreeViewNodeModel> tagChildren = [];
                foreach (long comicId in tagEntry.ComicIds)
                {
                    if (!comicMap.TryGetValue(comicId, out ComicModel? comic))
                    {
                        continue;
                    }

                    if (!tagMatched && !MatchSearchText(comic.Title))
                    {
                        continue;
                    }

                    SimpleTreeViewNodeModel comicNode = new()
                    {
                        DataContext = comic,
                        Glyph = "\uE8B9",
                        Title = comic.Title,
                        CanExpand = false,
                        Clicked = () =>
                        {
                            Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                                .WithParam(RouterConstants.ARG_COMIC_ID, comic.Id.ToString());
                            OpenInCurrentTabLiveData.Emit(route);
                        },
                        RequestContextMenuItemsAsync = (primary, selection) =>
                        {
                            IEnumerable<ComicModel> selectedComics = selection.Where(x => x.DataContext is ComicModel).Select(x => (ComicModel)x.DataContext!);
                            return MenuFlyoutItemsCreator.CreateComicMenuItems(comic, _actionHandler, selectedComics, canSelect: !SelectionMode);
                        },
                    };

                    tagChildren.Add(comicNode);
                }

                IOrderedEnumerable<SimpleTreeViewNodeModel> tagChildrenSorted = tagChildren.OrderBy(x => StringUtils.SmartFileNameKeySelector(x.Title), StringUtils.SmartFileNameComparer);
                foreach (SimpleTreeViewNodeModel child in tagChildrenSorted)
                {
                    tagNode.Children.Add(child);
                }

                tagNode.Description = $"({tagNode.Children.Count})";

                if (tagNode.Children.Count > 0 || tagMatched)
                {
                    tagCategoryNode.Children.Add(tagNode);
                }
            }

            tagCategoryNode.Description = $"({tagCategoryNode.Children.Count})";

            if (tagCategoryNode.Children.Count > 0)
            {
                dataSource.Add(tagCategoryNode);
            }
        }

        return dataSource;
    }

    private async Task<List<BaseMenuFlyoutItemModel>> CreateTagCategoryMenuItems(SimpleTreeViewNodeModel primary, IEnumerable<SimpleTreeViewNodeModel> selection)
    {
        List<BaseMenuFlyoutItemModel> items = [];

        if (primary.DataContext is not TagCateogryModel primaryCategory)
        {
            return items;
        }

        IEnumerable<TagCateogryModel> selectedCategories = selection
            .SelectMany(x => x.CollectDataContext<TagCateogryModel>());
        if (!selectedCategories.Any(x => x.Name == primaryCategory.Name))
        {
            selectedCategories = [primaryCategory];
        }

        items.Add(new SimpleMenuFlyoutItemModel(StringResourceProvider.Instance.Edit)
        {
            Glyph = "\uE70F",
            Click = () =>
            {
                EditTagCategoryLiveData.Emit(primaryCategory.Name);
            },
        });

        items.Add(new SimpleMenuFlyoutItemModel(StringResourceProvider.Instance.Delete)
        {
            Glyph = "\uE74D",
            Click = () =>
            {
                CoroutineUtils.Start(() => BusyStateManager.WithBusyState(async () =>
                {
                    await Task.WhenAll(selectedCategories.Select(x => TagCategoryInfoModel.Delete(x.Name)));
                }));
            },
        });

        List<ComicModel> comics = [.. primary.CollectDataContext<ComicModel>()];
        ComicModel? randomComic = comics.Count > 0 ? comics[Random.Shared.Next(comics.Count)] : null;
        return await MenuFlyoutItemsCreator.CreateComicGroupMenuItems(_actionHandler, randomComic,
            primary.ExpandAll, primary.CollapseAll, customItems: items);
    }

    private async Task<List<BaseMenuFlyoutItemModel>> CreateTagMenuItems(SimpleTreeViewNodeModel primary, IEnumerable<SimpleTreeViewNodeModel> selection)
    {
        List<BaseMenuFlyoutItemModel> items = [];

        if (primary.DataContext is not TagModel primaryTag)
        {
            return items;
        }

        IEnumerable<TagModel> selectedTags = selection.SelectMany(x => x.CollectDataContext<TagModel>());
        if (!selectedTags.Any(x => x.Name == primaryTag.Name))
        {
            selectedTags = [primaryTag];
        }

        items.Add(new SubItemMenuFlyoutItemModel(StringResourceProvider.Instance.Links)
        {
            Glyph = "\uE71B",
            Items = await MenuFlyoutItemsCreator.CreateTagLinkMenuItems(primaryTag.CategoryName, primaryTag.Name, _actionHandler),
        });

        items.Add(new SimpleMenuFlyoutItemModel(StringResourceProvider.Instance.Edit)
        {
            Glyph = "\uE70F",
            Click = () =>
            {
                EditTagLiveData.Emit(new(primaryTag.CategoryName, primaryTag.Name));
            },
        });

        items.Add(new SimpleMenuFlyoutItemModel(StringResourceProvider.Instance.Delete)
        {
            Glyph = "\uE74D",
            Click = () =>
            {
                CoroutineUtils.Start(() => BusyStateManager.WithBusyState(async () =>
                {
                    await Task.WhenAll(selectedTags.Select(x => TagInfoModel.Delete(x.CategoryName, x.Name)));
                }));
            },
        });

        List<ComicModel> comics = [.. primary.CollectDataContext<ComicModel>()];
        ComicModel? randomComic = comics.Count > 0 ? comics[Random.Shared.Next(comics.Count)] : null;
        return await MenuFlyoutItemsCreator.CreateComicGroupMenuItems(_actionHandler, randomComic,
            primary.ExpandAll, primary.CollapseAll, customItems: items);
    }

    private class TagCateogryEntry(string name, long comicId)
    {
        public string Name { get; } = name;
        public long ComicId { get; } = comicId;
        public HashSet<string> Tags { get; } = [];
    }

    private class TagEntry
    {
        public HashSet<long> ComicIds { get; } = [];
    }

    private class TagCateogryModel(string name)
    {
        public string Name { get; } = name;
    }

    private class TagModel(string categoryName, string name)
    {
        public string CategoryName { get; } = categoryName;
        public string Name { get; } = name;
    }
}
