// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

using ComicReader.Common;
using ComicReader.Common.Lifecycle;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.SDK.Common.Algorithm;
using ComicReader.ViewModels;

namespace ComicReader.Views.Pages.Reader;

internal partial class ReaderPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public readonly MutableLiveData<string> TagClickLiveData = new();
    public readonly MutableLiveData<KeyValuePair<string, string>> EditTagLiveData = new();

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

    public void LoadComicTag()
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
                    MenuFlyoutItems = CreateTagContextMenuItems(tags.Name, tag),
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

    private List<BaseMenuFlyoutItemViewModel> CreateTagContextMenuItems(string tagCategory, string tag)
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
}
