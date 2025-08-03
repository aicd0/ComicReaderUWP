// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Lifecycle;
using ComicReader.Common.Utils;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Data.Tables;
using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.Algorithm;
using ComicReader.SDK.Data.SqlHelpers;
using ComicReader.ViewModels;

namespace ComicReader.Views.Pages.SidePane.Tags;

internal partial class TagsPageViewModel : INotifyPropertyChanged
{
    private bool _updatingTags = false;
    private bool _updatingTagsInvalidated = false;

    public readonly MutableLiveData<Route> OpenInCurrentTabLiveData = new();
    public readonly MutableLiveData<Route> OpenInNewTabLiveData = new();
    public readonly MutableLiveData<List<ComicModel>> EditComicLiveData = new();
    public readonly MutableLiveData<string> EditTagCategoryLiveData = new();
    public readonly MutableLiveData<KeyValuePair<string, string>> EditTagLiveData = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<TreeNodeViewModel> DataSource { get; set; } = [];

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

    public void UpdateTags()
    {
        CoroutineUtils.Start(async () =>
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
        });
    }

    private async Task UpdateTagsInternal()
    {
        Dictionary<long, TagCateogryModel> tagCategoryMapper = [];
        await ComicData.Enqueue("UpdateTags", () =>
        {
            {
                var command = SelectCommand.Create(TagCategoryTable.Instance);
                IReaderToken<long> idToken = command.PutQueryInt64(TagCategoryTable.ColumnId);
                IReaderToken<string> nameToken = command.PutQueryString(TagCategoryTable.ColumnName);
                IReaderToken<long> comicIdToken = command.PutQueryInt64(TagCategoryTable.ColumnComicId);
                SelectCommand.IReader reader = command.Execute();
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
                SelectCommand.IReader reader = command.Execute();
                while (reader.Read())
                {
                    string content = contentToken.GetValue();
                    long categoryId = categoryIdToken.GetValue();
                    if (tagCategoryMapper.TryGetValue(categoryId, out TagCateogryModel? tagCategoryModel))
                    {
                        tagCategoryModel.Tags.Add(content);
                    }
                }
            }

            return true;
        });

        Dictionary<string, Dictionary<string, TagModel>> tagCategoryMap = [];
        TagModel PutTag(string tagCategory, string tag)
        {
            if (!tagCategoryMap.TryGetValue(tagCategory, out Dictionary<string, TagModel>? tags))
            {
                tags = [];
                tagCategoryMap[tagCategory] = tags;
            }

            if (!tags.TryGetValue(tag, out TagModel? tagModel))
            {
                tagModel = new();
                tags[tag] = tagModel;
            }

            return tagModel;
        }

        await TagInfoModel.Enqueue("UpdateTags", () =>
        {
            var command = SelectCommand.Create(TagInfoTable.Instance);
            IReaderToken<string> tagToken = command.PutQueryString(TagInfoTable.ColumnTag);
            IReaderToken<string> tagCategoryToken = command.PutQueryString(TagInfoTable.ColumnTagCategory);
            SelectCommand.IReader reader = command.Execute();
            while (reader.Read())
            {
                string tag = tagToken.GetValue();
                string tagCategory = tagCategoryToken.GetValue();
                PutTag(tagCategory, tag);
            }

            return true;
        });

        HashSet<long> requestingComicIds = [];
        foreach (TagCateogryModel tagCategoryModel in tagCategoryMapper.Values)
        {
            foreach (string tag in tagCategoryModel.Tags)
            {
                TagModel tagModel = PutTag(tagCategoryModel.Name, tag);
                tagModel.ComicIds.Add(tagCategoryModel.ComicId);
                requestingComicIds.Add(tagCategoryModel.ComicId);
            }
        }

        Dictionary<long, ComicModel> comicMap = [];
        {
            List<ComicModel> requestedComics = await ComicModel.BatchFromId("UpdateTags", requestingComicIds);
            foreach (ComicModel comic in requestedComics)
            {
                comicMap[comic.Id] = comic;
            }
        }

        List<TreeNodeViewModel> dataSource = [];
        List<string> tagCategories = [.. tagCategoryMap.Keys];
        tagCategories.Sort();
        foreach (string tagCategory in tagCategories)
        {
            Dictionary<string, TagModel> tagMap = tagCategoryMap[tagCategory];
            List<string> tags = [.. tagMap.Keys];
            tags.Sort();

            TreeNodeViewModel tagCategoryNode = new()
            {
                Glyph = "\uE8EC",
                Title = tagCategory,
                CanExpand = true,
                Expanded = true,
                MenuFlyoutItems = CreateTagCategoryMenuItems(tagCategory),
            };

            foreach (string tag in tags)
            {
                TagModel tagModel = tagMap[tag];

                TreeNodeViewModel tagNode = new()
                {
                    Glyph = "\uE8EC",
                    Title = tag,
                    CanExpand = true,
                    Expanded = false,
                    MenuFlyoutItems = CreateTagMenuItems(tagCategory, tag),
                };

                foreach (long comicId in tagModel.ComicIds)
                {
                    if (!comicMap.TryGetValue(comicId, out ComicModel? comic))
                    {
                        continue;
                    }

                    TreeNodeViewModel comicNode = new()
                    {
                        Glyph = "\uE8B9",
                        Title = comic.Title,
                        CanExpand = false,
                        MenuFlyoutItems = ComicItemMenuFlyoutCreator.CreateMenuItems(
                            comic, new ComicItemMenuFlyoutHandler(this, comic)),
                        OnClick = () =>
                        {
                            Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
                                .WithParam(RouterConstants.ARG_COMIC_ID, comic.Id.ToString());
                            OpenInCurrentTabLiveData.Emit(route);
                        },
                    };

                    tagNode.Children.Add(comicNode);
                }

                tagCategoryNode.Children.Add(tagNode);
            }

            dataSource.Add(tagCategoryNode);
        }

        void UpdateItem(TreeNodeViewModel from, TreeNodeViewModel to)
        {
            from.Glyph = to.Glyph;
            from.CanExpand = to.CanExpand;
            from.OnClick = to.OnClick;
            from.MenuFlyoutItems = to.MenuFlyoutItems;
            DiffUtils.UpdateCollection(from.Children, to.Children, (a, b) => a.Title == b.Title, UpdateItem);
        }

        DiffUtils.UpdateCollection(DataSource, dataSource, (x, y) => x.Title == y.Title, UpdateItem);
        NoTagsVisible = dataSource.Count == 0;
    }

    private List<BaseMenuFlyoutItemViewModel> CreateTagCategoryMenuItems(string tagCategory)
    {
        List<BaseMenuFlyoutItemViewModel> items = [];

        items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.Edit)
        {
            OnClick = () =>
            {
                EditTagCategoryLiveData.Emit(tagCategory);
            },
        });

        items.Add(new MenuFlyoutSeperatorViewModel());

        items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.Delete)
        {
            OnClick = () =>
            {
                _ = TagInfoModel.DeleteTagCategory(tagCategory);
            },
        });

        return items;
    }

    private List<BaseMenuFlyoutItemViewModel> CreateTagMenuItems(string tagCategory, string tag)
    {
        List<BaseMenuFlyoutItemViewModel> items = [];

        items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.Edit)
        {
            OnClick = () =>
            {
                EditTagLiveData.Emit(new(tagCategory, tag));
            },
        });

        items.Add(new MenuFlyoutSeperatorViewModel());

        items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.Delete)
        {
            OnClick = () =>
            {
                _ = TagInfoModel.DeleteTag(tagCategory, tag);
            },
        });

        return items;
    }

    private class TagCateogryModel(string name, long comicId)
    {
        public string Name { get; } = name;
        public long ComicId { get; } = comicId;
        public HashSet<string> Tags { get; } = [];
    }

    private class TagModel
    {
        public HashSet<long> ComicIds { get; } = [];
    }

    private class ComicItemMenuFlyoutHandler(TagsPageViewModel viewModel, ComicModel comic) : SimpleComicItemMenuFlyoutHandler(comic)
    {
        private readonly ComicModel _comic = comic;

        public override void OnEditClick()
        {
            viewModel.EditComicLiveData.Emit([_comic]);
        }

        protected override void OpenInNewTab(Route route)
        {
            viewModel.OpenInNewTabLiveData.Emit(route);
        }
    }
}
