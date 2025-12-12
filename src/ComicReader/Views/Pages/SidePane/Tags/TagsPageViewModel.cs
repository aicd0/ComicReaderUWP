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
using ComicReader.SDK.Data.SqlHelpers;
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

    public ObservableCollection<TagNodeViewModel> DataSource { get; set; } = [];

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
        List<TagNodeViewModel> dataSource = await GenerateNodeTree();

        void UpdateItem(TagNodeViewModel from, TagNodeViewModel to)
        {
            from.Glyph = to.Glyph;
            from.Description = to.Description;
            from.CanExpand = to.CanExpand;
            from.OnClick = to.OnClick;
            from.RequestContextFlyoutAsync = to.RequestContextFlyoutAsync;
            DiffUtils.UpdateCollection(from.Children, to.Children, (a, b) => a.Title == b.Title, UpdateItem);
        }

        DiffUtils.UpdateCollection(DataSource, dataSource, (x, y) => x.Title == y.Title, UpdateItem);
        NoTagsVisible = dataSource.Count == 0;
    }

    private async Task<List<TagNodeViewModel>> GenerateNodeTree()
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

        List<TagNodeViewModel> dataSource = [];
        List<string> tagCategories = [.. tagCategoryMap.Keys];
        tagCategories.Sort();
        foreach (string tagCategory in tagCategories)
        {
            Dictionary<string, TagEntry> tagMap = tagCategoryMap[tagCategory];
            List<string> tags = [.. tagMap.Keys];
            tags.Sort();

            TagCateogryModel tagCategoryModel = new(tagCategory);
            TagNodeViewModel tagCategoryNode = new()
            {
                DataContext = tagCategoryModel,
                Title = tagCategory,
                CanExpand = true,
                Expanded = true,
                RequestContextFlyoutAsync = selectedItems =>
                {
                    IEnumerable<TagCateogryModel> selectedModels = selectedItems.Where(x => x.DataContext is TagCateogryModel).Select(x => (TagCateogryModel)x.DataContext!);
                    List<BaseMenuFlyoutItemViewModel> result = CreateTagCategoryMenuItems(tagCategoryModel, selectedModels);
                    return Task.FromResult(result);
                },
            };

            foreach (string tag in tags)
            {
                TagEntry tagEntry = tagMap[tag];
                bool tagMatched = MatchSearchText(tag);

                TagModel tagModel = new(tagCategory, tag);
                TagNodeViewModel tagNode = new()
                {
                    DataContext = tagModel,
                    Glyph = "\uE8EC",
                    Title = tag,
                    CanExpand = true,
                    Expanded = false,
                    RequestContextFlyoutAsync = selectedItems =>
                    {
                        IEnumerable<TagModel> selectedModels = selectedItems.Where(x => x.DataContext is TagModel).Select(x => (TagModel)x.DataContext!);
                        return CreateTagMenuItems(tagModel, selectedModels);
                    },
                };

                List<TagNodeViewModel> tagChildren = [];
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

                    TagNodeViewModel comicNode = new()
                    {
                        DataContext = comic,
                        Glyph = "\uE8B9",
                        Title = comic.Title,
                        CanExpand = false,
                        OnClick = () =>
                        {
                            Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                                .WithParam(RouterConstants.ARG_COMIC_ID, comic.Id.ToString());
                            OpenInCurrentTabLiveData.Emit(route);
                        },
                        RequestContextFlyoutAsync = selectedItems =>
                        {
                            IEnumerable<ComicModel> selectedComics = selectedItems.Where(x => x.DataContext is ComicModel).Select(x => (ComicModel)x.DataContext!);
                            return MenuFlyoutItemsCreator.CreateMenuItems(comic, _actionHandler, selectedComics, canSelect: !SelectionMode);
                        },
                    };

                    tagChildren.Add(comicNode);
                }

                IOrderedEnumerable<TagNodeViewModel> tagChildrenSorted = tagChildren.OrderBy(x => StringUtils.SmartFileNameKeySelector(x.Title), StringUtils.SmartFileNameComparer);
                foreach (TagNodeViewModel child in tagChildrenSorted)
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

    private List<BaseMenuFlyoutItemViewModel> CreateTagCategoryMenuItems(TagCateogryModel primaryItem, IEnumerable<TagCateogryModel> selectedItems)
    {
        if (!selectedItems.Any(x => x.Name == primaryItem.Name))
        {
            selectedItems = [primaryItem];
        }

        List<BaseMenuFlyoutItemViewModel> items = [];

        items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.Edit)
        {
            Glyph = "\uE70F",
            OnClick = () =>
            {
                EditTagCategoryLiveData.Emit(primaryItem.Name);
            },
        });

        items.Add(new MenuFlyoutSeperatorViewModel());

        items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.Delete)
        {
            Glyph = "\uE74D",
            OnClick = () =>
            {
                CoroutineUtils.Start(async () =>
                {
                    foreach (TagCateogryModel item in selectedItems)
                    {
                        await TagCategoryInfoModel.Delete(item.Name);
                    }
                });
            },
        });

        if (!SelectionMode)
        {
            items.Add(new MenuFlyoutSeperatorViewModel());
            items.Add(MenuFlyoutItemsCreator.CreateSelectMenuItem(_actionHandler));
        }

        return items;
    }

    private async Task<List<BaseMenuFlyoutItemViewModel>> CreateTagMenuItems(TagModel primaryItem, IEnumerable<TagModel> selectedItems)
    {
        if (!selectedItems.Any(x => x.Name == primaryItem.Name))
        {
            selectedItems = [primaryItem];
        }

        List<BaseMenuFlyoutItemViewModel> items = [];

        items.Add(new MenuFlyoutSubItemViewModel(StringResourceProvider.Instance.Links)
        {
            Glyph = "\uE71B",
            Items = await MenuFlyoutItemsCreator.CreateTagLinkMenuItems(primaryItem.CategoryName, primaryItem.Name, _actionHandler),
        });

        items.Add(new MenuFlyoutSeperatorViewModel());

        items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.Edit)
        {
            Glyph = "\uE70F",
            OnClick = () =>
            {
                EditTagLiveData.Emit(new(primaryItem.CategoryName, primaryItem.Name));
            },
        });

        items.Add(new MenuFlyoutSeperatorViewModel());

        items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.Delete)
        {
            Glyph = "\uE74D",
            OnClick = () =>
            {
                CoroutineUtils.Start(async () =>
                {
                    foreach (TagModel item in selectedItems)
                    {
                        await TagInfoModel.Delete(item.CategoryName, item.Name);
                    }
                });
            },
        });

        if (!SelectionMode)
        {
            items.Add(new MenuFlyoutSeperatorViewModel());
            items.Add(MenuFlyoutItemsCreator.CreateSelectMenuItem(_actionHandler));
        }

        return items;
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
