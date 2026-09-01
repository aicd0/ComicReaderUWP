// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.Imaging;
using ComicReaderUWP.ViewModels;

using Microsoft.UI.Xaml;

namespace ComicReaderUWP.Views.Pages.Reader;

internal partial class ReaderPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private double _previewImageHeight;
    private double _previewImageWidth;

    // Comic Status
    private ComicModel? _comic;
    private LoadingComicInfo? _pendingComic;
    private bool _isLoading = false;
    private ComicConnection? _comicConnection;
    private int _pageIndex = -1;
    private bool? _isFavorite = null;

    public readonly MutableLiveData<string> TitleLiveData = new();
    public readonly MutableLiveData<bool> PlaybackChangeLiveData = new();
    public readonly MutableLiveData<ReaderPage.ReaderStatusInfo> ReaderStatusLiveData = new(new(ReaderPage.ReaderStatusEnum.Loading));
    public readonly MutableLiveData<bool> ComicChangedLiveData = new();
    public readonly MutableLiveData<bool> IsExternalComicLiveData = new(true);
    public readonly MutableLiveData<bool> IsFavoriteLiveData = new();
    public readonly MutableLiveData<ReaderLoadingInfo> ReaderLoadingInfoLiveData = new();

    private FlowDirection _preferredFlowDirection = FlowDirection.LeftToRight;
    public FlowDirection PreferredFlowDirection
    {
        get => _preferredFlowDirection;
        set
        {
            _preferredFlowDirection = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PreferredFlowDirection)));
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

    private string _zooming = string.Empty;
    public string Zooming
    {
        get => _zooming;
        set
        {
            _zooming = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Zooming)));
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
    public ReaderPage.ReaderStatusEnum ReaderStatus => ReaderStatusLiveData.Value.Status;
    public ComicModel? Comic => _comic;
    public ObservableCollection<ReaderImagePreviewViewModel> PreviewDataSource { get; set; } = [];
    public ReaderImagePreviewViewModel? SelectedPreview => (_pageIndex >= 0 && _pageIndex < PreviewDataSource.Count) ? PreviewDataSource[_pageIndex] : null;

    public ReaderPageViewModel() { }

    public void Initialize(double previewImageWidth, double previewImageHeight)
    {
        _previewImageWidth = previewImageWidth;
        _previewImageHeight = previewImageHeight;
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
        Playback.LoadState(playlist, serializedPlayback);
    }

    public async Task<IReadOnlyList<string>> GetImageDescriptions(IEnumerable<int> pageIndices)
    {
        ComicModel? comic = _comic;
        ComicConnection? comicConnection = _comicConnection;
        if (comic is null || comicConnection is null)
        {
            return [];
        }

        int imageCount = comicConnection.ImageCount;
        List<int> pageIndicesList = [.. pageIndices];
        pageIndicesList.RemoveAll(i => i < 0 || i >= imageCount);
        if (pageIndicesList.Count == 0)
        {
            return [];
        }

        pageIndicesList.Sort();

        List<string> imageDescriptions = [];
        foreach (int pageIndex in pageIndicesList)
        {
            string imageName = comicConnection.GetImageName(pageIndex);
            var imageSource = new ComicImageSource(comic, comicConnection, pageIndex);
            ImageMeta? imageMeta = await ImageLoader.LoadImageMeta(imageSource, new()
            {
                Priority = ImageLoadingPriority.READER_IMAGE,
            });

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
            return [];
        }

        return imageDescriptions;
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

    public void SetZooming(int zooming)
    {
        Zooming = $"{zooming}%";
    }

    public void UpdateFavoriteStatus()
    {
        ComicModel? comic = _comic;
        if (comic is not null)
        {
            UpdateFavoriteStatusInternal(comic);
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
        else if (playlistItem.Comic != _comic || reason == PlaybackModel.StatusChangeReason.Refresh)
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
        CoroutineUtils.Run(async () =>
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

        // Close previous comic
        CloseComicConnection();
        _comic = null;
        PreviewDataSource.Clear();

        if (comic is null)
        {
            ReaderStatusLiveData.Emit(new(ReaderPage.ReaderStatusEnum.Error));
            return;
        }

        _comic = comic;

        // Save history
        if (!comic.IsExternal)
        {
            await comic.SetAsVisited();
            if (AppSettingsModel.Instance.SaveBrowsingHistory)
            {
                await ComicHistoryItemModel.AddAsync(comic.Id, comic.Title1);
            }
        }

        IsExternalComicLiveData.Emit(comic.IsExternal);
        UpdateFavoriteStatusInternal(comic);

        // Load reader images
        ComicConnection? connection = await comic.OpenComic();
        if (connection is null)
        {
            ReaderStatusLiveData.Emit(new(ReaderPage.ReaderStatusEnum.Error));
            return;
        }

        _comicConnection = connection;
        ReaderStatusLiveData.Emit(new(ReaderPage.ReaderStatusEnum.Loading));

        var images = new List<IImageSource>();
        for (int i = 0; i < connection.ImageCount; ++i)
        {
            images.Add(new ComicImageSource(comic, connection, i));
        }

        if (images.Count == 0)
        {
            ReaderStatusLiveData.Emit(new(ReaderPage.ReaderStatusEnum.Error));
            return;
        }

        CompletionStatusEnum oldCompletionStatus = comic.CompletionStatus;
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
                    bool restorePosition = AppSettingsModel.Instance.RestoreLastReadingPosition &&
                        !(AppSettingsModel.Instance.RestoreLastReadingPositionOnlyAppliesToReadingComics && oldCompletionStatus != CompletionStatusEnum.Reading);
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
        for (int i = 0; i < connection.ImageCount; ++i)
        {
            PreviewDataSource.Add(new ReaderImagePreviewViewModel
            {
                Image = new SimpleImageView.Model
                {
                    Source = new ComicImageSource(comic, connection, i),
                    Width = _previewImageWidth,
                    Height = _previewImageHeight,
                    DebugDescription = i.ToString(),
                },
                Page = i + 1,
            });
        }
    }

    private void UpdateFavoriteStatusInternal(ComicModel comic)
    {
        bool isFavorite = !comic.IsExternal && FavoriteModel.Instance.FromId(comic.Id) != null;
        SetIsFavorite(isFavorite, false);
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
