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

using Microsoft.UI.Xaml;

namespace ComicReaderUWP.Views.Pages.Reader;

internal partial class ReaderPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private ActionHandler _actionHandler = ActionHandler.Dummy;
    private readonly ITaskDispatcher _loadPreviewDispatcher = TaskDispatcher.Factory.NewQueue("ReaderLoadPreview");

    // Comic states
    private ComicModel? _comic;
    private LoadingComicInfo? _pendingComic;
    private bool _isLoading = false;
    private IComicConnection? _comicConnection;
    private int _pageIndex = -1;
    private bool? _isFavorite = null;

    public readonly MutableLiveData<string> TitleLiveData = new();
    public readonly MutableLiveData<bool> PlaybackChangeLiveData = new();
    public readonly MutableLiveData<KeyValuePair<string, string>> EditTagLiveData = new();
    public readonly MutableLiveData<ReaderPage.ReaderStatusInfo> ReaderStatusLiveData = new(new(ReaderPage.ReaderStatusEnum.Loading));
    public readonly MutableLiveData<ComicModel> ReaderSettingLiveData = new();
    public readonly MutableLiveData<bool> IsExternalComicLiveData = new(true);
    public readonly MutableLiveData<string> ComicDescriptionLiveData = new();
    public readonly MutableLiveData<bool> IsFavoriteLiveData = new();
    public readonly MutableLiveData<ComicCompletionStatusEnum> CompletionStateLiveData = new();
    public readonly MutableLiveData<ReaderLoadingInfo> ReaderLoadingInfoLiveData = new();

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

    private string _imageDescription = string.Empty;
    public string ImageDescription
    {
        get => _imageDescription;
        set
        {
            _imageDescription = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageDescription)));
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

    private string _primaryPageIndicatorText = string.Empty;
    public string PrimaryPageIndicatorText
    {
        get => _primaryPageIndicatorText;
        set
        {
            _primaryPageIndicatorText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PrimaryPageIndicatorText)));
        }
    }

    private string _secondaryPageIndicatorText = string.Empty;
    public string SecondaryPageIndicatorText
    {
        get => _secondaryPageIndicatorText;
        set
        {
            _secondaryPageIndicatorText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SecondaryPageIndicatorText)));
        }
    }

    private bool _isAutoPlayEnabled = false;
    public bool IsAutoPlayEnabled
    {
        get => _isAutoPlayEnabled;
        set
        {
            _isAutoPlayEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAutoPlayEnabled)));
        }
    }

    private bool _isAutoPlaying = false;
    public bool IsAutoPlaying
    {
        get => _isAutoPlaying;
        set
        {
            _isAutoPlaying = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAutoPlaying)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlayOrPauseButtonTooltip)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlayOrPauseButtonGlyph)));
        }
    }

    public string PlayOrPauseButtonTooltip
    {
        get => _isAutoPlaying ? StringResourceProvider.Instance.Pause : StringResourceProvider.Instance.Play;
    }

    public string PlayOrPauseButtonGlyph
    {
        get => _isAutoPlaying ? "\uE769" : "\uE768";
    }

    private bool _isPlaybackNextEnabled = false;
    public bool IsPlaybackNextEnabled
    {
        get => _isPlaybackNextEnabled;
        set
        {
            _isPlaybackNextEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPlaybackNextEnabled)));
        }
    }

    private bool _isPlaybackPreviousEnabled = false;
    public bool IsPlaybackPreviousEnabled
    {
        get => _isPlaybackPreviousEnabled;
        set
        {
            _isPlaybackPreviousEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPlaybackPreviousEnabled)));
        }
    }

    public PlaylistModel Playlist { get; private set; } = PlaylistModel.CreateEmpty();
    public PlaybackModel Playback { get; } = new();
    public ReaderPage.ReaderStatusEnum ReaderStatus => ReaderStatusLiveData.GetValue()?.Status ?? ReaderPage.ReaderStatusEnum.Loading;
    public ComicModel? Comic => _comic;
    public ObservableCollection<TagCollectionViewModel> ComicTags { get; } = [];
    public ObservableCollection<ReaderImagePreviewViewModel> PreviewDataSource { get; set; } = [];
    public bool IsFavorite => _isFavorite ?? false;

    public ReaderPageViewModel() { }

    public void Initialize(ActionHandler actionHandler)
    {
        _actionHandler = actionHandler;
        Playback.PlaybackStatusChanged += Playback_PlaybackStatusChanged;
    }

    public void Destory()
    {
        Playback.PlaybackStatusChanged -= Playback_PlaybackStatusChanged;
        CloseComicConnection();
    }

    public void LoadPlaylist(PlaylistModel playlist, string? serializedPlayback)
    {
        Playlist = playlist;
        Playback.SetPlaylist(playlist, serializedPlayback);
    }

    public void SetIsFavorite(bool isFavorite, bool writeDatabase)
    {
        if (_isFavorite == isFavorite)
        {
            return;
        }

        _isFavorite = isFavorite;
        IsFavoriteLiveData.Emit(isFavorite);

        ComicModel? comic = _comic;
        if (writeDatabase && comic != null && !comic.IsExternal)
        {
            if (isFavorite)
            {
                FavoriteModel.Instance.Add(comic.Id, comic.Title1, true);
            }
            else
            {
                FavoriteModel.Instance.RemoveWithId(comic.Id, true);
            }
        }
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
            CoroutineUtils.Start(async () =>
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

    public void SetPageIndex(int pageIndex)
    {
        if (pageIndex < 0)
        {
            pageIndex = -1;
        }

        if (pageIndex == _pageIndex)
        {
            return;
        }

        _pageIndex = pageIndex;
        UpdateImageDescription();
    }

    public void ReloadComicInfo()
    {
        LoadComicInfo();
    }

    public void ReloadReaderSettings()
    {
        LoadReaderSettings();
    }

    public void AddNewTags(string command)
    {
        ComicModel? comic = _comic;
        if (comic is null || string.IsNullOrWhiteSpace(command))
        {
            return;
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

        CoroutineUtils.Start(() => comic.SetTags(tags));
    }

    private void Playback_PlaybackStatusChanged(PlaybackModel.StatusChangeReason reason)
    {
        IsPlaybackNextEnabled = Playback.CanGoNext;
        IsPlaybackPreviousEnabled = Playback.CanGoPrevious;

        PlaylistModel.PlaylistItem? playlistItem = Playback.CurrentItem;
        if (playlistItem is null)
        {
            TitleLiveData.Emit(StringResourceProvider.Instance.Error);
            ReaderStatusLiveData.Emit(new(ReaderPage.ReaderStatusEnum.Error));
        }
        else if (playlistItem.Comic != _comic)
        {
            TitleLiveData.Emit(playlistItem.Comic.Title);
            ReaderStatusLiveData.Emit(new(ReaderPage.ReaderStatusEnum.Loading));
            LoadComic(new()
            {
                Comic = playlistItem.Comic,
                LoadReason = reason,
            });
        }

        PlaybackChangeLiveData.Emit(true);
    }

    private void CloseComicConnection()
    {
        _comicConnection?.Dispose();
        _comicConnection = null;
    }

    private void LoadComic(LoadingComicInfo comic)
    {
        CoroutineUtils.Start(async () =>
        {
            if (_isLoading)
            {
                _pendingComic = comic;
                return;
            }

            _isLoading = true;
            try
            {
                LoadingComicInfo? loadingComic = comic;
                while (loadingComic != null)
                {
                    await LoadComicInternal(loadingComic);
                    loadingComic = _pendingComic;
                    _pendingComic = null;
                }
            }
            finally
            {
                _isLoading = false;
            }
        });
    }

    private async Task LoadComicInternal(LoadingComicInfo info)
    {
        ComicModel? comic = info.Comic;
        if (comic == _comic)
        {
            return;
        }

        // Close previous comic
        CloseComicConnection();
        _comic = null;
        PreviewDataSource.Clear();

        // Load new comic
        if (comic is null)
        {
            ReaderStatusLiveData.Emit(new(ReaderPage.ReaderStatusEnum.Error));
            return;
        }

        _comic = comic;

        if (!comic.IsExternal)
        {
            await comic.SetCompletionStateToAtLeastStarted();
            if (AppSettingsModel.Instance.SaveBrowsingHistory)
            {
                await ComicHistoryItemModel.AddAsync(comic.Id, comic.Title1);
            }
        }

        LoadReaderSettings();
        LoadComicInfo();

        IComicConnection? connection = await comic.OpenComicAsync();
        if (connection is null)
        {
            ReaderStatusLiveData.Emit(new(ReaderPage.ReaderStatusEnum.Error));
            return;
        }

        _comicConnection = connection;
        ReaderStatusLiveData.Emit(new(ReaderPage.ReaderStatusEnum.Loading));

        var images = new List<IImageSource>();
        for (int i = 0; i < connection.GetImageCount(); ++i)
        {
            images.Add(new ComicImageSource(connection, i));
        }

        if (images.Count == 0)
        {
            ReaderStatusLiveData.Emit(new(ReaderPage.ReaderStatusEnum.Error));
            return;
        }

        double initialPage;
        switch (info.LoadReason)
        {
            case PlaybackModel.StatusChangeReason.Next:
            case PlaybackModel.StatusChangeReason.Previous:
                initialPage = 1.0;
                break;
            case PlaybackModel.StatusChangeReason.PreviousByOverScroll:
                initialPage = comic.PageCount;
                break;
            default:
                {
                    bool restorePosition = AppSettingsModel.Instance.GetModel().RestoreLastReadingPosition && !comic.IsExternal;
                    if (restorePosition)
                    {
                        double lastPosition = comic.LastPosition;
                        initialPage = lastPosition > 1E-2 ? lastPosition : 1.0;
                    }
                    else
                    {
                        initialPage = 1.0;
                    }
                }
                break;
        }

        ReaderLoadingInfoLiveData.Emit(new(images, initialPage));

        // Load preview images
        double previewWidth = (double)Application.Current.Resources["ReaderPreviewImageWidth"];
        double previewHeight = (double)Application.Current.Resources["ReaderPreviewImageHeight"];
        for (int i = 0; i < connection.GetImageCount(); ++i)
        {
            PreviewDataSource.Add(new ReaderImagePreviewViewModel
            {
                Image = new SimpleImageView.Model
                {
                    Source = new ComicImageSource(connection, i),
                    Width = previewWidth,
                    Height = previewHeight,
                    Dispatcher = _loadPreviewDispatcher,
                    DebugDescription = i.ToString()
                },
                Page = i + 1,
            });
        }
    }

    private void LoadReaderSettings()
    {
        ComicModel? comic = _comic;
        if (comic == null)
        {
            return;
        }

        ReaderSettingLiveData.Emit(comic);
    }

    private void LoadComicInfo()
    {
        ComicModel? comic = _comic;
        if (comic == null)
        {
            return;
        }

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

        bool isFavorite = !comic.IsExternal && FavoriteModel.Instance.FromId(comic.Id) != null;
        SetIsFavorite(isFavorite, false);

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

        items.Add(new SeparatorMenuFlyoutItemModel());

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Delete,
            Glyph = "\uE74D",
            Click = () =>
            {
                CoroutineUtils.Start(async () =>
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

    private void UpdateImageDescription()
    {
        ComicModel? comic = _comic;
        IComicConnection? comicConnection = _comicConnection;
        int pageIndex = _pageIndex;
        if (comic is null || pageIndex < 0 || comicConnection is null)
        {
            ImageDescription = string.Empty;
            return;
        }

        int imageCount = comicConnection.GetImageCount();
        if (pageIndex >= imageCount)
        {
            ImageDescription = string.Empty;
            return;
        }

        string imageName = comicConnection.GetImageName(pageIndex);
        var imageSource = new ComicImageSource(comicConnection, pageIndex);
        TaskDispatcher.DefaultQueue.Submit("LoadImageMeta", () =>
        {
            ImageCacheManager.ImageMeta? imageMeta = ImageCacheManager.GetImageMeta(imageSource);
            StringBuilder imageDescriptionSb = new();

            if (!string.IsNullOrEmpty(imageName))
            {
                imageDescriptionSb.Append(imageName);
            }

            if (imageMeta is not null)
            {
                if (imageDescriptionSb.Length > 0)
                {
                    imageDescriptionSb.Append('\n');
                }

                imageDescriptionSb.Append(imageMeta.Format);
                imageDescriptionSb.Append(' ').Append(imageMeta.Width).Append(" x ").Append(imageMeta.Height);
                imageDescriptionSb.Append(' ').Append(FormatBytes(imageMeta.Size));

                if (imageMeta.DpiX > 0 && imageMeta.DpiY > 0)
                {
                    imageDescriptionSb.Append(' ').Append(FormatDpi(imageMeta.DpiX, imageMeta.DpiY));
                }

                imageDescriptionSb.Append(' ').Append(imageMeta.BitsPerPixel).Append(" bits");
            }

            string imageDescription = imageDescriptionSb.ToString();
            CoroutineUtils.RunInMainThread(() =>
            {
                ImageDescription = imageDescription;
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

    //
    // Types
    //

    public class ReaderLoadingInfo(IEnumerable<IImageSource> images, double initialPage)
    {
        public readonly IEnumerable<IImageSource> Images = images;
        public readonly double InitialPage = initialPage;
    }

    private class LoadingComicInfo
    {
        public required ComicModel? Comic { get; init; }
        public required PlaybackModel.StatusChangeReason LoadReason { get; init; }
    }
}
