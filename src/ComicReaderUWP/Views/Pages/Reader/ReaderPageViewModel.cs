// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.Imaging;
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
    public readonly MutableLiveData<ReaderPage.ReaderStatusInfo> ReaderStatusLiveData = new(new(ReaderPage.ReaderStatusEnum.Loading));
    public readonly MutableLiveData<bool> ComicChangedLiveData = new();
    public readonly MutableLiveData<bool> IsExternalComicLiveData = new(true);
    public readonly MutableLiveData<bool> IsFavoriteLiveData = new();
    public readonly MutableLiveData<ReaderLoadingInfo> ReaderLoadingInfoLiveData = new();

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

    private string _fullscreenButtonGlyph = string.Empty;
    public string FullscreenButtonGlyph
    {
        get => _fullscreenButtonGlyph;
        set
        {
            _fullscreenButtonGlyph = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullscreenButtonGlyph)));
        }
    }

    private string _fullscreenButtonText = string.Empty;
    public string FullscreenButtonText
    {
        get => _fullscreenButtonText;
        set
        {
            _fullscreenButtonText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullscreenButtonText)));
        }
    }

    private bool _isPinned = false;
    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            _isPinned = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPinned)));
        }
    }

    private string _pinButtonGlyph = string.Empty;
    public string PinButtonGlyph
    {
        get => _pinButtonGlyph;
        set
        {
            _pinButtonGlyph = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PinButtonGlyph)));
        }
    }

    private string _pinButtonText = string.Empty;
    public string PinButtonText
    {
        get => _pinButtonText;
        set
        {
            _pinButtonText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PinButtonText)));
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
    public ObservableCollection<ReaderImagePreviewViewModel> PreviewDataSource { get; set; } = [];
    public ReaderImagePreviewViewModel? SelectedPreview => (_pageIndex >= 0 && _pageIndex < PreviewDataSource.Count) ? PreviewDataSource[_pageIndex] : null;

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

        if (_pageIndex >= 0 && _pageIndex < PreviewDataSource.Count)
        {
            PreviewDataSource[_pageIndex].Selected = false;
        }

        _pageIndex = pageIndex;

        if (pageIndex < PreviewDataSource.Count)
        {
            PreviewDataSource[pageIndex].Selected = true;
        }
    }

    public void SetFullscreen(bool isFullscreen)
    {
        IsFullscreen = isFullscreen;
        if (isFullscreen)
        {
            FullscreenButtonGlyph = "\uE73F";
            FullscreenButtonText = StringResourceProvider.Instance.ExitFullscreen;
        }
        else
        {
            FullscreenButtonGlyph = "\uE740";
            FullscreenButtonText = StringResourceProvider.Instance.EnterFullscreen;
        }
    }

    public void SetPinned(bool pinned)
    {
        IsPinned = pinned;
        if (pinned)
        {
            PinButtonGlyph = "\uE77A";
            PinButtonText = StringResourceProvider.Instance.Unpin;
        }
        else
        {
            PinButtonGlyph = "\uE718";
            PinButtonText = StringResourceProvider.Instance.Pin;
        }
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

            ComicChangedLiveData.Emit(true);
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

        IsExternalComicLiveData.Emit(comic.IsExternal);
        if (!comic.IsExternal)
        {
            await comic.SetCompletionStateToAtLeastStarted();
            if (AppSettingsModel.Instance.SaveBrowsingHistory)
            {
                await ComicHistoryItemModel.AddAsync(comic.Id, comic.Title1);
            }
        }

        bool isFavorite = !comic.IsExternal && FavoriteModel.Instance.FromId(comic.Id) != null;
        SetIsFavorite(isFavorite, false);

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

        bool useScrollingAreaStartEnd = AppSettingsModel.Instance.UseScrollingAreaAsStartEnd;
        double startPage = useScrollingAreaStartEnd ? 0.5 : 1.0;
        double endPage = useScrollingAreaStartEnd ? comic.PageCount + 0.5 : images.Count;
        double initialPage;
        switch (info.LoadReason)
        {
            case PlaybackModel.StatusChangeReason.Next:
            case PlaybackModel.StatusChangeReason.Previous:
                initialPage = startPage;
                break;
            case PlaybackModel.StatusChangeReason.PreviousByOverScroll:
                initialPage = endPage;
                break;
            default:
                {
                    bool restorePosition = AppSettingsModel.Instance.RestoreLastReadingPosition && !comic.IsExternal;
                    if (restorePosition)
                    {
                        double lastPosition = comic.LastPosition;
                        initialPage = lastPosition > 0 ? lastPosition : startPage;
                    }
                    else
                    {
                        initialPage = startPage;
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
