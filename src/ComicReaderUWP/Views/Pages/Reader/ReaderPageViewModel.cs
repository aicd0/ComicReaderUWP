// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Actions.Components;
using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Common.ErrorHandling;
using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Data.Models.Playback;
using ComicReaderUWP.Helpers.Imaging;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.SDK.Models;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Views.Pages.Reader;

internal partial class ReaderPageViewModel : INotifyPropertyChanged
{
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

    private ActionHandler _actionHandler = ActionHandler.Dummy;
    private double _previewImageHeight;
    private double _previewImageWidth;

    // Comic Status
    private ComicModel? _comic;
    private LoadComicArgs? _pendingComic;
    private bool _isLoading = false;
    private ComicConnection? _comicConnection;
    private readonly List<ReaderPreviewImageViewModel> _selectedPreviewImages = [];
    private bool? _isFavorite = null;

    public ReaderPageViewModel() { }

    public event PropertyChangedEventHandler? PropertyChanged;

    public readonly MutableLiveData<string> TitleLiveData = new();
    public readonly MutableLiveData<bool> PlaybackChangeLiveData = new();
    public readonly MutableLiveData<ReaderPage.ReaderStatusInfo> ReaderStatusLiveData = new(new() { Status = ReaderPage.ReaderStatusEnum.Loading });
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
    public ObservableCollection<ReaderPreviewImageViewModel> PreviewDataSource { get; set; } = [];
    public IEnumerable<ReaderPreviewImageViewModel> SelectedPreviews => _selectedPreviewImages;

    public void Initialize(ActionHandler actionHandler, double previewImageWidth, double previewImageHeight)
    {
        _actionHandler = actionHandler;
        _previewImageWidth = previewImageWidth;
        _previewImageHeight = previewImageHeight;
        Playback.PlaybackStateChanged += Playback_PlaybackStateChanged;
    }

    public void Destory()
    {
        Playback.PlaybackStateChanged -= Playback_PlaybackStateChanged;
        CloseComicConnection();
    }

    public void LoadPlaylist(PlaylistModel playlist, string? serializedPlayback, double initialPage)
    {
        Playlist = playlist;
        Playback.LoadState(playlist, serializedPlayback, initialPage);
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

    public void SetPageIndices(IReadOnlySet<int> pageIndices)
    {
        for (int i = _selectedPreviewImages.Count - 1; i >= 0; --i)
        {
            ReaderPreviewImageViewModel previewImage = _selectedPreviewImages[i];
            if (!pageIndices.Contains(previewImage.Page - 1))
            {
                previewImage.Selected = false;
                _selectedPreviewImages.RemoveAt(i);
            }
        }

        foreach (int index in pageIndices)
        {
            if (index < 0 || index >= PreviewDataSource.Count)
            {
                continue;
            }

            ReaderPreviewImageViewModel previewImage = PreviewDataSource[index];
            if (_selectedPreviewImages.Contains(previewImage))
            {
                continue;
            }

            int position = _selectedPreviewImages.Count;
            while (position > 0 && _selectedPreviewImages[position - 1].Page > previewImage.Page)
            {
                --position;
            }

            _selectedPreviewImages.Insert(position, previewImage);
            previewImage.Selected = true;
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

    public async Task<IReadOnlyList<BaseMenuFlyoutItemModel>> CreateImageContextMenuItems(int index, IImageSource imageSource)
    {
        ComicModel? comic = Comic;
        if (comic is null)
        {
            return [];
        }

        using IImageConnection? imageConnection = await imageSource.Open();
        if (imageConnection is null)
        {
            return [];
        }

        string imagePath = imageConnection.Path;

        List<BaseMenuFlyoutItemModel> items = [];

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.Copy,
            Icon = new FontIconSource() { Glyph = "\uE8C8" },
            Click = () =>
            {
                CoroutineUtils.Run(async () =>
                {
                    ErrorResult err = await ErrorLogger.Run($"{nameof(CreateImageContextMenuItems)}#Copy", async err =>
                    {
                        using IImageConnection? connection = await imageSource.Open();
                        if (connection is null)
                        {
                            return err.Error("Failed to open image connection.");
                        }

                        using Stream? stream = await connection.OpenImageStream();
                        if (stream is null)
                        {
                            return err.Error("Failed to open image stream.");
                        }

                        ErrorResult innerErr = await ClipboardUtils.SetImage(stream);
                        if (!innerErr.IsSuccessful)
                        {
                            return err.Error(innerErr);
                        }

                        return err.Success();
                    });

                    err.DisplayErrorMessage(_actionHandler);
                });
            },
        });

        items.Add(new SimpleMenuFlyoutItemModel()
        {
            Text = StringResourceProvider.Instance.ShowInFileExplorer,
            Icon = new FontIconSource() { Glyph = "\uE838" },
            IsEnabled = !string.IsNullOrEmpty(imagePath),
            Click = () =>
            {
                CoroutineUtils.Run(async () =>
                {
                    ErrorResult err = await ThirdPartyLauncher.ShowInFileExplorer(imagePath);
                    err.DisplayErrorMessage(_actionHandler);
                });
            }
        });

        if (!comic.IsExternal)
        {
            string? coverIndexString = comic.GetExt(ComicExt.COVER_INDEX);
            if (string.IsNullOrEmpty(coverIndexString) || !int.TryParse(coverIndexString, out int coverIndex))
            {
                coverIndex = 0;
            }

            items.Add(new SimpleMenuFlyoutItemModel()
            {
                Text = StringResourceProvider.Instance.SetAsCover,
                Icon = new FontIconSource() { Glyph = "\uE82D" },
                IsEnabled = coverIndex != index,
                Click = () =>
                {
                    CoroutineUtils.Run(async () =>
                    {
                        comic.SetExt(ComicExt.COVER_INDEX, index.ToString(CultureInfo.InvariantCulture));
                        comic.SetExt(ComicExt.COVER_CACHE_KEY, null);
                        await comic.FlushExt();
                    });
                },
            });
        }

        {
            List<BaseMenuFlyoutItemModel> fileOperationItems = [];

            fileOperationItems.Add(new SimpleMenuFlyoutItemModel()
            {
                Text = StringResourceProvider.Instance.Copy,
                Click = () =>
                {
                    CoroutineUtils.Run(async () =>
                    {
                        ErrorResult err = await ErrorLogger.Run($"{nameof(CreateImageContextMenuItems)}#CopyFile", async err =>
                        {
                            if (!string.IsNullOrEmpty(imagePath))
                            {
                                ErrorResult innerErr = await ClipboardUtils.SetFile(imagePath);
                                if (!innerErr.IsSuccessful)
                                {
                                    return err.Error(innerErr);
                                }

                                return err.Success();
                            }

                            ImageMeta? meta = await ImageLoader.LoadImageMeta(imageSource, new());
                            string extension = meta?.Format.ToLowerInvariant() switch
                            {
                                "jpeg" or "jpg" => ".jpg",
                                "png" => ".png",
                                "bmp" => ".bmp",
                                "gif" => ".gif",
                                "tiff" => ".tiff",
                                "heif" or "heic" => ".heic",
                                "webp" => ".webp",
                                _ => string.Empty,
                            };

                            using IImageConnection? connection = await imageSource.Open();
                            if (connection is null)
                            {
                                return err.Error("Failed to open image connection.");
                            }

                            using Stream? stream = await connection.OpenImageStream();
                            if (stream is null)
                            {
                                return err.Error("Failed to open image stream.");
                            }

                            ErrorResult streamErr = await ClipboardUtils.SetFile(stream, $"{index + 1}{extension}");
                            if (!streamErr.IsSuccessful)
                            {
                                return err.Error(streamErr);
                            }

                            return err.Success();
                        });

                        err.DisplayErrorMessage(_actionHandler);
                    });
                },
            });

            fileOperationItems.Add(new SimpleMenuFlyoutItemModel()
            {
                Text = StringResourceProvider.Instance.Delete,
                IsEnabled = !string.IsNullOrEmpty(imagePath),
                Click = () =>
                {
                    CoroutineUtils.Run(async () =>
                    {
                        string promptContent = StringResourceProvider.Instance.DeleteFilesPrompt
                            .Replace("$files", imagePath);
                        DialogOptions options = new DialogOptions.Builder()
                            .SetTitle(StringResourceProvider.Instance.Warning)
                            .SetContent(promptContent)
                            .SetPrimaryButtonText(StringResourceProvider.Instance.Delete)
                            .SetCloseButtonText(StringResourceProvider.Instance.Cancel)
                            .Build();

                        int windowId = -1;
                        if (_actionHandler.TryGetComponent(out IMainWindowComponent? mainWindowCom))
                        {
                            windowId = mainWindowCom.WindowId;
                        }

                        DialogResult result = await DialogUtils.EnqueueDialogAsync(windowId, options);
                        if (result != DialogResult.Primary)
                        {
                            return;
                        }

                        ErrorResult err = ErrorLogger.Run($"{nameof(CreateImageContextMenuItems)}#DeleteFile", err =>
                        {
                            try
                            {
                                File.Delete(imagePath);
                            }
                            catch (Exception ex)
                            {
                                return err.Error(ex);
                            }

                            return err.Success();
                        });

                        err.DisplayErrorMessage(_actionHandler);

                        if (err.IsSuccessful)
                        {
                            Playback.Refresh();
                        }
                    });
                },
            });

            items.Add(new SubItemMenuFlyoutItemModel()
            {
                Text = StringResourceProvider.Instance.FileOperations,
                Icon = new FontIconSource() { Glyph = "\uE91B" },
                Items = fileOperationItems,
            });
        }

        return items;
    }

    public async Task<IReadOnlyList<BaseMenuFlyoutItemModel>> CreateComicContextMenuItems()
    {
        ComicModel? comic = Comic;
        if (comic is null)
        {
            return [];
        }

        return await MenuFlyoutItemsCreator.CreateComicMenuItems(
            _actionHandler,
            comic,
            playlist: Playlist.ToBuilder(),
            playback: Playback.ToBuilder());
    }

    private void Playback_PlaybackStateChanged(PlaybackStateChangedEventArgs args)
    {
        IsPlaybackNextEnabled = Playback.CanGoNext;
        IsPlaybackPreviousEnabled = Playback.CanGoPrevious;

        PlaylistModel.PlaylistItem? playlistItem = Playback.CurrentItem;
        if (playlistItem is null)
        {
            TitleLiveData.Emit(StringResourceProvider.Instance.Error);
            ReaderStatusLiveData.Emit(new()
            {
                Status = ReaderPage.ReaderStatusEnum.Error,
                Description = "The reading list is empty.",
            });
        }
        else if (playlistItem.Comic != _comic || args.Reason == PlaybackStateChangeReason.Refresh)
        {
            TitleLiveData.Emit(playlistItem.Comic.Title);
            ReaderStatusLiveData.Emit(new() { Status = ReaderPage.ReaderStatusEnum.Loading });
            LoadComic(new()
            {
                Comic = playlistItem.Comic,
                PlaybackArgs = args,
            });
        }

        PlaybackChangeLiveData.Emit(true);
    }

    private void CloseComicConnection()
    {
        _comicConnection?.Dispose();
        _comicConnection = null;
    }

    private void LoadComic(LoadComicArgs args)
    {
        CoroutineUtils.Run(async () =>
        {
            if (_isLoading)
            {
                _pendingComic = args;
                return;
            }

            _isLoading = true;
            try
            {
                LoadComicArgs? loadingComic = args;
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

    private async Task LoadComicInternal(LoadComicArgs args)
    {
        ComicModel? comic = args.Comic;

        // Close and clear previous comic
        CloseComicConnection();
        _comic = null;

        // Clear preview images
        foreach (ReaderPreviewImageViewModel previewImage in _selectedPreviewImages)
        {
            previewImage.Selected = false;
        }

        _selectedPreviewImages.Clear();
        PreviewDataSource.Clear();

        // Load new comic
        if (comic is null)
        {
            ReaderStatusLiveData.Emit(new()
            {
                Status = ReaderPage.ReaderStatusEnum.Error,
                Description = "No comic is selected.",
            });
            return;
        }

        _comic = comic;
        CompletionStatusEnum completionStatus = comic.CompletionStatus;

        // Save to history
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

        // Open new comic
        ErrorResult<ComicConnection> connectionErr = await comic.OpenComic();
        if (!connectionErr.IsSuccessful)
        {
            ReaderStatusLiveData.Emit(new()
            {
                Status = ReaderPage.ReaderStatusEnum.Error,
                Description = connectionErr.DetailedErrorMessage,
            });
            return;
        }

        ComicConnection connection = connectionErr.Result;
        _comicConnection = connection;
        ReaderStatusLiveData.Emit(new() { Status = ReaderPage.ReaderStatusEnum.Loading });

        int imageCount = connection.ImageCount;
        if (imageCount == 0)
        {
            ReaderStatusLiveData.Emit(new()
            {
                Status = ReaderPage.ReaderStatusEnum.Error,
                Description = "No images found.",
            });
            return;
        }

        List<IImageSource> images = new(imageCount);
        for (int i = 0; i < imageCount; ++i)
        {
            images.Add(new ComicImageSource(comic, connection, i));
        }

        // Load preview images
        for (int i = 0; i < images.Count; ++i)
        {
            int index = i;
            IImageSource imageSource = images[i];
            PreviewDataSource.Add(new()
            {
                Image = new SimpleImageView.Model
                {
                    Source = imageSource,
                    Width = _previewImageWidth,
                    Height = _previewImageHeight,
                    DebugDescription = i.ToString(),
                },
                Page = i + 1,
                RequestContextMenu = async () => await CreateImageContextMenuItems(index, imageSource),
            });
        }

        // Load reader images
        double overrideInitialPage = args.PlaybackArgs.InitialPage;
        bool useScrollingAreaStartEnd = AppSettingsModel.Instance.UseScrollingAreaAsStartEnd;
        double startPage = useScrollingAreaStartEnd ? 0.5 : 1.0;
        double endPage = useScrollingAreaStartEnd ? images.Count + 0.5 : images.Count;

        double initialPage;
        if (double.IsFinite(overrideInitialPage) && overrideInitialPage >= 0.0)
        {
            initialPage = Math.Clamp(overrideInitialPage, startPage, endPage);
        }
        else
        {
            switch (args.PlaybackArgs.Reason)
            {
                case PlaybackStateChangeReason.Next:
                case PlaybackStateChangeReason.Previous:
                    initialPage = startPage;
                    break;

                case PlaybackStateChangeReason.PreviousByOverScroll:
                    initialPage = endPage;
                    break;

                default:
                    {
                        bool restorePosition =
                            AppSettingsModel.Instance.RestoreLastReadingPosition &&
                            !(AppSettingsModel.Instance.RestoreLastReadingPositionOnlyAppliesToReadingComics && completionStatus != CompletionStatusEnum.Reading);
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
        }

        ReaderLoadingInfoLiveData.Emit(new(images, initialPage));
    }

    private void UpdateFavoriteStatusInternal(ComicModel comic)
    {
        bool isFavorite = !comic.IsExternal && FavoriteModel.Instance.FromId(comic.Id) != null;
        SetIsFavorite(isFavorite, false);
    }

    //
    // Types
    //

    public class ReaderLoadingInfo(IEnumerable<IImageSource> images, double initialPage)
    {
        public readonly IEnumerable<IImageSource> Images = images;
        public readonly double InitialPage = initialPage;
    }

    private class LoadComicArgs
    {
        public required ComicModel? Comic { get; init; }
        public required PlaybackStateChangedEventArgs PlaybackArgs { get; init; }
    }
}
