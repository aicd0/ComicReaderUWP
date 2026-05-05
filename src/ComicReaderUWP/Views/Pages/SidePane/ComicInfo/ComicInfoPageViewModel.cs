// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Actions.Providers;
using ComicReaderUWP.Common.Expression;
using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.Imaging;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.Helpers.Search;
using ComicReaderUWP.SDK.Common.Algorithm;
using ComicReaderUWP.SDK.Common.Lifecycle;
using ComicReaderUWP.SDK.Common.Threading;
using ComicReaderUWP.SDK.Common.Utils;
using ComicReaderUWP.ViewModels;

namespace ComicReaderUWP.Views.Pages.SidePane.ComicInfo;

internal partial class ComicInfoPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isEmpty = true;
    public bool IsEmpty
    {
        get => _isEmpty;
        set
        {
            _isEmpty = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEmpty)));
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

    private string _comicTitle1 = string.Empty;
    public string ComicTitle1
    {
        get => _comicTitle1;
        set
        {
            _comicTitle1 = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComicTitle1)));
        }
    }

    private string _comicTitle2 = string.Empty;
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

    private bool _hasAnyTags = false;
    public bool HasAnyTags
    {
        get => _hasAnyTags;
        set
        {
            _hasAnyTags = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasAnyTags)));
        }
    }

    public ComicModel? Comic => _comic;
    public PlaylistModel Playlist { get; private set; } = PlaylistModel.CreateEmpty();
    public bool IsComicTitle2Visible => ComicTitle2.Length > 0;
    public ObservableCollection<TagCollectionViewModel> ComicTags { get; } = [];
    public ObservableCollection<string> ImageDescriptions { get; } = [];

    public readonly MutableLiveData<string> ComicDescriptionLiveData = new();
    public readonly MutableLiveData<bool> IsExternalComicLiveData = new(true);
    public readonly MutableLiveData<ComicCompletionStatusEnum> CompletionStateLiveData = new();
    public readonly MutableLiveData<KeyValuePair<string, string>> EditTagLiveData = new();

    private ActionHandler _actionHandler = ActionHandler.Dummy;
    private ComicModel? _comic;
    private readonly HashSet<int> _pageIndices = [];

    public void Initialize(ActionHandler actionHandler)
    {
        _actionHandler = actionHandler;
    }

    public void SetComic(ComicModel? comic)
    {
        if (_comic == comic)
        {
            return;
        }

        _comic = comic;
        _pageIndices.Clear();
        LoadComicInfo();
        CoroutineUtils.Run(UpdateImageDescription);
    }

    public void SetPageIndices(ISet<int> pageIndices)
    {
        if (_pageIndices.SetEquals(pageIndices))
        {
            return;
        }

        _pageIndices.Clear();
        foreach (int i in pageIndices)
        {
            _pageIndices.Add(i);
        }

        CoroutineUtils.Run(UpdateImageDescription);
    }

    public void SetPlaylist(PlaylistModel playlist)
    {
        Playlist = playlist;
    }

    public void SetCompletionState(ComicCompletionStatusEnum completionState)
    {
        ComicModel? comic = _comic;
        if (comic is null)
        {
            return;
        }

        if (comic.CompletionState != completionState && !comic.IsExternal)
        {
            CoroutineUtils.Run(async () =>
            {
                switch (completionState)
                {
                    case ComicCompletionStatusEnum.NotStarted:
                        await comic.SetCompletionStateToNotStarted();
                        break;
                    case ComicCompletionStatusEnum.Started:
                        await comic.SetCompletionStateToStarted();
                        break;
                    case ComicCompletionStatusEnum.Completed:
                        await comic.SetCompletionStateToCompleted();
                        break;
                    default:
                        break;
                }
            });
        }

        CompletionStateLiveData.Emit(comic.CompletionState);
    }

    public bool AddNewTags(string command)
    {
        ComicModel? comic = _comic;
        if (comic is null || string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        command = command.ReplaceLineEndings(string.Empty);
        string key = string.Empty;
        string value = command;
        bool overwriteMode = false;
        for (int i = 0; i < command.Length; i++)
        {
            char c = command[i];
            if (LocalizationUtils.Commas.Contains(c))
            {
                break;
            }
            else if (LocalizationUtils.Colons.Contains(c))
            {
                key = command[..i];
                overwriteMode = i + 1 < command.Length && LocalizationUtils.Colons.Contains(command[i + 1]);
                value = command[(overwriteMode ? i + 2 : i + 1)..];
                break;
            }
        }

        key = key.Trim();
        if (string.IsNullOrEmpty(key))
        {
            key = StringResourceProvider.Instance.Default;
        }

        string[] values = value.Split(LocalizationUtils.Commas, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Dictionary<string, HashSet<string>> tags = comic.TagsCopy;
        if (!tags.TryGetValue(key, out HashSet<string>? categoryTags))
        {
            categoryTags = [];
            tags.Add(key, categoryTags);
        }

        if (overwriteMode)
        {
            categoryTags.Clear();
        }

        foreach (string tag in values)
        {
            categoryTags.Add(tag);
        }

        CoroutineUtils.Run(() => comic.SetTags(tags));
        return true;
    }

    public void Reload()
    {
        LoadComicInfo();
    }

    private void LoadComicInfo()
    {
        ComicModel? comic = _comic;
        if (comic == null)
        {
            IsEmpty = true;
            return;
        }

        IsEmpty = false;
        IsExternalComicLiveData.Emit(comic.IsExternal);

        if (comic.Title1.Length == 0)
        {
            ComicTitle1 = comic.Title;
        }
        else
        {
            ComicTitle1 = comic.Title1;
            ComicTitle2 = comic.Title2;
        }

        ComicDescriptionLiveData.Emit(comic.Description);

        ComicDir = comic.Location;
        IsEditable = comic.IsEditable;

        LoadComicTag();
        SetCompletionState(comic.CompletionState);

        if (!comic.IsExternal)
        {
            int rating = comic.Rating;
            Rating = rating >= 0 ? rating * 0.05F : -1.0;
        }
    }

    private void LoadComicTag()
    {
        ComicModel? comic = _comic;
        if (comic == null)
        {
            return;
        }

        List<TagCollectionViewModel> newCollection = [];
        for (int i = 0; i < comic.Tags.Count; ++i)
        {
            ComicHandle.TagData tags = comic.Tags[i];
            List<TagViewModel> tagModels = [];
            foreach (string tag in tags.Tags)
            {
                TagViewModel tagModel = new()
                {
                    Tag = tag,
                    OnClicked = () =>
                    {
                        string expression = $"%{ComicSQLProviderUtils.VAR_TAG}.\"{ExpressionUtils.EscapeString(tags.Name)}\"=\"{ExpressionUtils.EscapeString(tag)}\"";
                        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SEARCH)
                            .WithParam(RouterConstants.ARG_KEYWORD, $"exp:\"{ExpressionUtils.EscapeString(expression)}\"");
                        ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                            .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
                            .AddParameter(OpenTabProvider.PARAM_TAB_ID, string.Empty)
                            .Build();
                        _actionHandler.Handle(actionModel);
                    },
                    OnRequestContextFlyoutAsync = () =>
                    {
                        return CreateTagContextMenuItems(tags.Name, tag);
                    },
                };

                tagModels.Add(tagModel);
            }

            tagModels.Sort((a, b) => string.Compare(a.Tag, b.Tag, ignoreCase: true));
            var tagCollectionModel = new TagCollectionViewModel(tags.Name);
            foreach (TagViewModel tag in tagModels)
            {
                tagCollectionModel.Tags.Add(tag);
            }

            newCollection.Add(tagCollectionModel);
        }

        newCollection.Sort((a, b) => string.Compare(a.Name, b.Name, ignoreCase: true));
        DiffUtils.UpdateCollection(ComicTags, newCollection, (x, y) => x.Name == y.Name, (x, y) =>
        {
            DiffUtils.UpdateCollection(x.Tags, y.Tags, (a, b) => a.Tag == b.Tag, (a, b) =>
            {
                a.OnRequestContextFlyoutAsync = b.OnRequestContextFlyoutAsync;
                a.OnClicked = b.OnClicked;
            });
        });

        HasAnyTags = ComicTags.Count > 0;
    }

    private async Task<List<BaseMenuFlyoutItemModel>> CreateTagContextMenuItems(string tagCategory, string tag)
    {
        List<BaseMenuFlyoutItemModel> items = [];

        items.Add(new SubItemMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Links,
            Glyph = "\uE71B",
            Items = await MenuFlyoutItemsCreator.CreateTagLinkMenuItems(tagCategory, tag, _actionHandler),
        });

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Edit,
            Glyph = "\uE70F",
            Click = () =>
            {
                EditTagLiveData.Emit(new(tagCategory, tag));
            },
        });

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Delete,
            Glyph = "\uE74D",
            Click = () =>
            {
                CoroutineUtils.Run(async () =>
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
                            await comic.SetTags(tags);
                        }
                    }
                });
            },
        });

        return items;
    }

    private async Task UpdateImageDescription()
    {
        void ClearDescription()
        {
            CoroutineUtils.RunInMainThread(ImageDescriptions.Clear);
        }

        ComicModel? comic = _comic;
        List<int> pageIndices = [.. _pageIndices];
        if (comic is null || pageIndices.Count == 0)
        {
            ClearDescription();
            return;
        }

        TaskDispatcher.DefaultQueue.Submit("LoadImageMeta", () =>
        {
            using IComicConnection? comicConnection = comic.OpenComicAsync().Result;
            if (comicConnection is null)
            {
                ClearDescription();
                return;
            }

            int imageCount = comicConnection.GetImageCount();
            pageIndices.RemoveAll(i => i < 0 || i >= imageCount);
            if (pageIndices.Count == 0)
            {
                ClearDescription();
                return;
            }

            pageIndices.Sort();

            List<string> imageDescriptions = [];
            foreach (int pageIndex in pageIndices)
            {
                string imageName = comicConnection.GetImageName(pageIndex);
                var imageSource = new ComicImageSource(comicConnection, pageIndex);
                ImageCacheManager.ImageMeta? imageMeta = ImageCacheManager.GetImageMeta(imageSource);

                if (imageMeta is null)
                {
                    continue;
                }

                StringBuilder imageDescriptionSb = new();
                if (string.IsNullOrEmpty(imageName))
                {
                    imageName = StringResourceProvider.Instance.PageN.Replace("$page", (pageIndex + 1).ToString());
                }

                imageDescriptionSb.Append(imageName).Append('\n');
                imageDescriptionSb.Append(imageMeta.Format);
                imageDescriptionSb.Append(' ').Append(imageMeta.Width).Append(" x ").Append(imageMeta.Height);
                imageDescriptionSb.Append(' ').Append(FormatBytes(imageMeta.Size));

                if (imageMeta.DpiX > 0 && imageMeta.DpiY > 0)
                {
                    imageDescriptionSb.Append(' ').Append(FormatDpi(imageMeta.DpiX, imageMeta.DpiY));
                }

                imageDescriptionSb.Append(' ').Append(imageMeta.BitsPerPixel).Append(" bits");
                imageDescriptions.Add(imageDescriptionSb.ToString());
            }

            if (imageDescriptions.Count == 0)
            {
                ClearDescription();
                return;
            }

            CoroutineUtils.RunInMainThread(() =>
            {
                ImageDescriptions.Clear();
                foreach (string imageDescription in imageDescriptions)
                {
                    ImageDescriptions.Add(imageDescription);
                }
            });
        });
    }

    private static string FormatDpi(double dpiX, double dpiY)
    {
        if (dpiX == dpiY)
        {
            return $"{dpiX:0.##} dpi";
        }
        else
        {
            return $"{dpiX:0.##} x {dpiY:0.##} dpi";
        }
    }

    private static string FormatBytes(long byteCount)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];
        if (byteCount < 1024)
        {
            return $"{byteCount} B";
        }

        int unitIndex = (int)Math.Floor(Math.Log(byteCount, 1024));
        double adjustedSize = byteCount / Math.Pow(1024, unitIndex);
        return $"{adjustedSize:0.#} {units[unitIndex]}";
    }
}
