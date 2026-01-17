// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Data.Models.TagInfo;
using ComicReaderUWP.Data.Tables;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Helpers.Misc;
using ComicReaderUWP.SDK.Common.Algorithm;
using ComicReaderUWP.SDK.Common.Lifecycle;
using ComicReaderUWP.SDK.Common.Utils;
using ComicReaderUWP.SDK.Database.SqlHelpers;
using ComicReaderUWP.ViewModels;

namespace ComicReaderUWP.Views.Pages.SidePane.Tags;

internal partial class TagsPageViewModel : INotifyPropertyChanged
{
    private const int SEARCH_DELAY = 200;

    public event PropertyChangedEventHandler? PropertyChanged;

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
        await ComicHandle.Enqueue("UpdateTags", () =>
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

        SimpleTreeViewNodeModel ComicToNode(ComicModel comic, PlaylistModel.Builder playlist)
        {
            return new()
            {
                DataContext = comic,
                Glyph = "\uE8B9",
                Title = comic.Title,
                CanExpand = false,
                Clicked = item =>
                {
                    OpenComicHelper.OpenComic(_actionHandler, OpenComicHelper.GetComicRoute(comic, playlist));
                },
                RequestContextMenuItemsAsync = (primary, selection) =>
                {
                    IEnumerable<ComicModel> selectedComics = selection
                        .Where(x => x.DataContext is ComicModel)
                        .Select(x => (ComicModel)x.DataContext!);
                    return MenuFlyoutItemsCreator.CreateComicMenuItems(
                        _actionHandler, comic, playlist,
                        selectedComics, canSelect: !SelectionMode);
                },
            };
        }

        List<SimpleTreeViewNodeModel> dataSource = [];
        IEnumerable<string> tagCategories = tagCategoryMap.Keys
            .OrderBy(StringUtils.SmartFileNameKeySelector, StringUtils.SmartFileNameComparer);
        foreach (string tagCategory in tagCategories)
        {
            Dictionary<string, TagEntry> tagMap = tagCategoryMap[tagCategory];
            IEnumerable<string> tags = tagMap.Keys
                .OrderBy(StringUtils.SmartFileNameKeySelector, StringUtils.SmartFileNameComparer);

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

                IEnumerable<ComicModel> sortedComics = tagEntry.ComicIds
                    .Where(comicMap.ContainsKey)
                    .Select(x => comicMap[x])
                    .Where(x => tagMatched || MatchSearchText(x.Title))
                    .OrderBy(x => StringUtils.SmartFileNameKeySelector(x.Title), StringUtils.SmartFileNameComparer);
                PlaylistModel.Builder playlist = PlaylistModel.Builder.Create().AddComics(sortedComics);
                IEnumerable<SimpleTreeViewNodeModel> tagChildren = sortedComics
                    .Select(x => ComicToNode(x, playlist));

                foreach (SimpleTreeViewNodeModel child in tagChildren)
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

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Edit,
            Glyph = "\uE70F",
            Click = () =>
            {
                EditTagCategoryLiveData.Emit(primaryCategory.Name);
            },
        });

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Delete,
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
        return await MenuFlyoutItemsCreator.CreateComicGroupMenuItems(
            _actionHandler, comics,
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

        items.Add(new SubItemMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Links,
            Glyph = "\uE71B",
            Items = await MenuFlyoutItemsCreator.CreateTagLinkMenuItems(primaryTag.CategoryName, primaryTag.Name, _actionHandler),
        });

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Edit,
            Glyph = "\uE70F",
            Click = () =>
            {
                EditTagLiveData.Emit(new(primaryTag.CategoryName, primaryTag.Name));
            },
        });

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Delete,
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
        return await MenuFlyoutItemsCreator.CreateComicGroupMenuItems(
            _actionHandler, comics,
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
