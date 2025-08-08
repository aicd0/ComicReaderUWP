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
using ComicReader.Data.Models.Comic;
using ComicReader.Data.Models.TagInfo;
using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.SDK.Common.Algorithm;
using ComicReader.ViewModels;

namespace ComicReader.Views.Pages.Reader;

internal partial class ReaderPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public readonly MutableLiveData<string> TagClickLiveData = new();
    public readonly MutableLiveData<KeyValuePair<string, string>> EditTagLiveData = new();
    public readonly MutableLiveData<DialogUtils.DialogOptions> ShowDialogLiveData = new();

    private string _comicTitle1 = "";
    public string ComicTitle1
    {
        get => _comicTitle1;
        set
        {
            _comicTitle1 = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComicTitle1)));
        }
    }

    private string _comicTitle2 = "";
    public string ComicTitle2
    {
        get => _comicTitle2;
        set
        {
            _comicTitle2 = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComicTitle2)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsComicTitle2Visible)));
        }
    }

    public bool IsComicTitle2Visible => ComicTitle2.Length > 0;

    private string _comicDir = "";
    public string ComicDir
    {
        get => _comicDir;
        set
        {
            _comicDir = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComicDir)));
        }
    }

    private bool _isComicTagsVisible = false;
    public bool IsComicTagsVisible
    {
        get => _isComicTagsVisible;
        set
        {
            _isComicTagsVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsComicTagsVisible)));
        }
    }

    private bool _isEditable;
    public bool IsEditable
    {
        get => _isEditable;
        set
        {
            _isEditable = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEditable)));
        }
    }

    private double _rating;
    public double Rating
    {
        get => _rating;
        set
        {
            _rating = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Rating)));
        }
    }

    private bool _isFullscreen = false;
    public bool IsFullscreen
    {
        get => _isFullscreen;
        set
        {
            _isFullscreen = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFullscreen)));
        }
    }

    public ObservableCollection<TagCollectionViewModel> ComicTags { get; } = [];
    public ObservableCollection<ReaderImagePreviewViewModel> PreviewDataSource { get; set; } = [];

    private ComicModel? _comic;

    public ReaderPageViewModel() { }

    public void SetComic(ComicModel comic)
    {
        _comic = comic;
    }

    public async Task LoadComicTag()
    {
        ComicModel? comic = _comic;
        if (comic == null)
        {
            return;
        }

        var newCollection = new ObservableCollection<TagCollectionViewModel>();

        for (int i = 0; i < comic.Tags.Count; ++i)
        {
            ComicData.TagData tags = comic.Tags[i];
            var tagCollectionModel = new TagCollectionViewModel(tags.Name);
            foreach (string tag in tags.Tags)
            {
                TagViewModel tagModel = new()
                {
                    Tag = tag,
                    MenuFlyoutItems = await CreateTagContextMenuItems(tags.Name, tag),
                    OnClicked = () =>
                    {
                        TagClickLiveData.Emit(tag);
                    },
                };

                tagCollectionModel.Tags.Add(tagModel);
            }

            newCollection.Add(tagCollectionModel);
        }

        DiffUtils.UpdateCollection(ComicTags, newCollection, (x, y) => x.Name == y.Name, (x, y) =>
        {
            DiffUtils.UpdateCollection(x.Tags, y.Tags, (a, b) => a.Tag == b.Tag, (a, b) =>
            {
                a.MenuFlyoutItems = b.MenuFlyoutItems;
                a.OnClicked = b.OnClicked;
            });
        });

        IsComicTagsVisible = newCollection.Count > 0;
    }

    private async Task<List<BaseMenuFlyoutItemViewModel>> CreateTagContextMenuItems(string tagCategory, string tag)
    {
        List<BaseMenuFlyoutItemViewModel> items = [];

        {
            TagCategoryInfoModel? tagCategoryInfo = await TagCategoryInfoModel.Get(tagCategory);
            TagInfoModel? tagInfo = await TagInfoModel.Get(tagCategory, tag);
            List<TagLinkModel.LinkModel> links = [];

            if (tagCategoryInfo != null)
            {
                var linkModel = TagLinkModel.Parse(tagCategoryInfo.GetExt(TagCategoryInfoExt.LINKS));
                links.AddRange(linkModel.Links);
            }

            if (tagInfo != null)
            {
                var linkModel = TagLinkModel.Parse(tagInfo.GetExt(TagInfoExt.LINKS));
                links.AddRange(linkModel.Links);
            }

            foreach (TagLinkModel.LinkModel link in links)
            {
                string tagEscaped = Uri.EscapeDataString(tag);
                string tagCategoryEscaped = Uri.EscapeDataString(tagCategory);
                link.Link = link.Link
                    .Replace("{%tag}", tag)
                    .Replace("{%tag_category}", tagCategory)
                    .Replace("{%tag_escaped}", tagEscaped)
                    .Replace("{%tag_category_escaped}", tagCategoryEscaped);
            }

            if (links.Count > 0)
            {
                links.Sort((a, b) => a.Name.CompareTo(b.Name));

                foreach (TagLinkModel.LinkModel link in links)
                {
                    items.Add(new MenuFlyoutItemViewModel(link.Name)
                    {
                        Glyph = "\uE71B",
                        OnClick = () =>
                        {
                            if (StringUtils.TryNormalizeWebUrl(link.Link, out Uri? uri))
                            {
                                _ = Windows.System.Launcher.LaunchUriAsync(uri);
                            }
                            else
                            {
                                ShowDialogLiveData.Emit(new DialogUtils.DialogOptions.Builder()
                                    .SetTitle(StringResourceProvider.Instance.LinkErrorTitle)
                                    .SetContent(StringResourceProvider.Instance.LinkErrorContent.Replace("$link", link.Link))
                                    .SetPrimaryButtonText(StringResourceProvider.Instance.OK)
                                    .Build());
                            }
                        }
                    });
                }

                items.Add(new MenuFlyoutSeperatorViewModel());
            }
        }

        items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.Edit)
        {
            Glyph = "\uE70F",
            OnClick = () =>
            {
                EditTagLiveData.Emit(new(tagCategory, tag));
            },
        });

        items.Add(new MenuFlyoutSeperatorViewModel());

        items.Add(new MenuFlyoutItemViewModel(StringResourceProvider.Instance.Delete)
        {
            Glyph = "\uE74D",
            OnClick = () =>
            {
                ComicModel? comic = _comic;
                if (comic == null)
                {
                    return;
                }

                Dictionary<string, HashSet<string>> tags = comic.TagsCopy;
                if (tags.TryGetValue(tagCategory, out HashSet<string>? tagSet))
                {
                    if (tagSet.Remove(tag))
                    {
                        comic.SetTags(tags);
                    }
                }
            },
        });

        return items;
    }
}
