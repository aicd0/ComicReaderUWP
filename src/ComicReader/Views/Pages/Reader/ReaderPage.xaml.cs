// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.BaseUI;
using ComicReader.Common.Constants;
using ComicReader.Common.Imaging;
using ComicReader.Common.Legacy;
using ComicReader.Common.Lifecycle;
using ComicReader.Common.Utils;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.Imaging;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.KVStorage;
using ComicReader.SDK.Common.Threading;
using ComicReader.ViewModels;
using ComicReader.Views.Dialogs.EditComicInfo;
using ComicReader.Views.Dialogs.EditTag;
using ComicReader.Views.Pages.Main;
using ComicReader.Views.Pages.Navigation;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;

using Windows.Storage;

namespace ComicReader.Views.Pages.Reader;

internal sealed partial class ReaderPage : BasePage
{
    //
    // Constants
    //

    private const string TAG = nameof(ReaderPage);
    private const string KEY_TIP_SHOWN = "ReaderTipShown";
    private const string REGEX_URL = @"https?:\/\/[a-zA-Z0-9\-._~%]+(?:\.[a-zA-Z0-9\-._~%]+)+(?:\/[^\s]*)?";

    //
    // Variables
    //

    private ComicModel? _comic;
    private ComicModel? _pendingComic;
    private bool _isLoading = false;

    private volatile bool _updatingProgress = false;
    private bool? _isFavorite = null;
    private ComicCompletionStatusEnum? _completionState = null;

    private bool _bottomTileShowed = false;
    private bool _bottomTileHold = false;
    private long _bottomTileTargetHideTime = -1;

    private readonly ITaskDispatcher _loadPreviewDispatcher = TaskDispatcher.Factory.NewQueue("ReaderLoadPreview");

    private MutableLiveData<ReaderStatusEnum> ReaderStatusLiveData { get; } = new(ReaderStatusEnum.Loading);

    private readonly MutableLiveData<bool> _isExternalComicLiveData = new(true);
    public LiveData<bool> IsExternalComicLiveData => _isExternalComicLiveData;

    private bool _gridViewModeEnabled = false;
    private bool GridViewModeEnabled
    {
        get => _gridViewModeEnabled;
        set
        {
            _gridViewModeEnabled = value;
            UpdateReaderUI();
            GetNavigationPageAbility().SetGridViewMode(value);
        }
    }

    private ReaderPageViewModel ViewModel { get; set; } = new();

    //
    // Constructor
    //

    public ReaderPage()
    {
        InitializeComponent();

        ViewModel.ComicTitle1 = "";
        ViewModel.ComicTitle2 = "";
        ViewModel.ComicDir = "";
        ViewModel.IsEditable = false;
        ViewModel.PreviewDataSource = new ObservableCollection<ReaderImagePreviewViewModel>();

        ReaderView reader = MainReaderView;

        reader.ReaderEventTapped += delegate (ReaderView sender)
        {
            BottomTileSetHold(!_bottomTileShowed);
        };

        reader.ReaderEventPageChanged += delegate (ReaderView sender, bool isIntermediate)
        {
            ViewModel.SetPageIndex(MainReaderView.CurrentPageDisplay - 1);
            UpdatePage();
            UpdateProgress(sender, save: !isIntermediate);
            BottomTileSetHold(false);
        };

        reader.ReaderEventReaderStateChanged += delegate (ReaderView sender, ReaderView.ReaderState state)
        {
            switch (state)
            {
                case ReaderView.ReaderState.Ready:
                    ReaderStatusLiveData.Emit(ReaderStatusEnum.Working);
                    UpdatePage();
                    ShowBottomTile();
                    HideBottomTileDelayed(5000);
                    break;
                case ReaderView.ReaderState.Loading:
                    ReaderStatusLiveData.Emit(ReaderStatusEnum.Loading);
                    break;
                case ReaderView.ReaderState.Error:
                    ReaderStatusLiveData.Emit(ReaderStatusEnum.Error);
                    break;
            }
        };
    }

    //
    // Lifecycle
    //

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);

        bool tipShown = KVDatabase.Default.GetBoolean(DatabaseEntry.KV_LIB_TIPS, KEY_TIP_SHOWN, false);
        if (!tipShown)
        {
            ReaderTip.IsOpen = !tipShown;
        }

        GetMainPageAbility().SetIcon(new SymbolIconSource { Symbol = Symbol.Pictures });
        CoroutineUtils.Start(async () =>
        {
            ComicModel? comic = await GetTargetComic(bundle);
            if (comic != null)
            {
                GetMainPageAbility().SetTitle(comic.Title);
                await LoadComic(comic);
            }
        });
    }

    protected override void OnResume()
    {
        base.OnResume();

        ObserveData();
        GetNavigationPageAbility().SetGridViewMode(false);
        LoadReaderSettings();
        UpdateReaderUI();
        LoadComicInfo();
    }

    protected override void OnStop()
    {
        base.OnStop();
        ViewModel.CloseComicConnection();
    }

    private void ObserveData()
    {
        GlobalEvent.Instance.ComicUpdated.Observe(this, delegate
        {
            LoadComicInfo();
        });

        GlobalEvent.Instance.FavoriteUpdated.Observe(this, delegate
        {
            LoadComicInfo();
        });

        GlobalEvent.Instance.TagInfoUpdated.Observe(this, delegate
        {
            LoadComicInfo();
        });

        GetEventBus().With<double>(EventId.TitleBarHeightChange).ObserveSticky(this, delegate (double h)
        {
            TitleBarArea.Height = h;
            PreviewTitleBarPlaceHolder.Height = h;
        });

        GetEventBus().With<double>(EventId.TitleBarOpacity).ObserveSticky(this, delegate (double opacity)
        {
            BottomGrid.Opacity = opacity;
        });

        GetMainPageAbility().RegisterTitleBarVisibilityChangedHandler(this, delegate (bool visible)
        {
            if (visible)
            {
                ShowBottomTile();
            }
            else
            {
                HideBottomTile();
            }
        });

        GetMainPageAbility().RegisterFullscreenChangedHandler(this, delegate (bool isFullscreen)
        {
            ViewModel.IsFullscreen = isFullscreen;
        });

        GetNavigationPageAbility().RegisterGridViewModeChangedHandler(this, delegate (bool enabled)
        {
            GridViewModeEnabled = enabled;
        });

        GetNavigationPageAbility().RegisterExpandInfoPaneHandler(this, delegate
        {
            if (InfoPane != null)
            {
                InfoPane.IsPaneOpen = true;
            }
        });

        GetNavigationPageAbility().RegisterReaderSettingsChangedEventHandler(this, delegate (ReaderSettingDataModel setting)
        {
            ComicModel? comic = _comic;
            if (comic != null && !comic.IsExternal)
            {
                setting.To(comic);
            }

            ApplyReaderSettings(setting);
            UpdateReaderUI();
        });

        GetNavigationPageAbility().RegisterFavoriteChangedEventHandler(this, delegate (bool isFavorite)
        {
            SetIsFavorite(isFavorite, true);
        });

        ViewModel.TagClickLiveData.Observe(this, tag =>
        {
            Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SEARCH)
                .WithParam(RouterConstants.ARG_KEYWORD, "<tag: " + tag + ">");
            GetMainPageAbility().OpenInNewTab(route);
        });

        ViewModel.EditTagLiveData.Observe(this, pair =>
        {
            var dialog = new EditTagDialog(pair.Key, pair.Value);
            _ = dialog.ShowAsync(XamlRoot);
        });

        ViewModel.ShowDialogLiveData.Observe(this, options =>
        {
            _ = DialogUtils.ShowDialogAsync(XamlRoot, options);
        });

        IsExternalComicLiveData.ObserveSticky(this, delegate (bool isExternal)
        {
            RcRating.Visibility = isExternal ? Visibility.Collapsed : Visibility.Visible;
            FavoriteBt.IsEnabled = !isExternal;
            SetCompletionStateButton.Visibility = isExternal ? Visibility.Collapsed : Visibility.Visible;
            GetNavigationPageAbility().SetExternalComic(isExternal);
        });

        ReaderStatusLiveData.Observe(this, delegate (ReaderStatusEnum status)
        {
            string readerStatusText = "";
            readerStatusText = status switch
            {
                ReaderStatusEnum.Loading => StringResourceProvider.Instance.ReaderStatusLoading,
                ReaderStatusEnum.Error => StringResourceProvider.Instance.ReaderStatusError,
                _ => "",
            };
            TbReaderStatus.Text = readerStatusText;
            TbReaderStatus.Visibility = readerStatusText.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateReaderUI();
        });
    }

    private async Task<ComicModel?> GetTargetComic(PageBundle bundle)
    {
        if (!long.TryParse(bundle.GetString(RouterConstants.ARG_COMIC_ID, "-1"), out long comicId))
        {
            comicId = -1;
        }

        if (comicId > 0)
        {
            ComicModel? comic = await ComicModel.FromId(comicId, "GetTargetComic");
            if (comic is not null)
            {
                return comic;
            }
        }

        string location = bundle.GetString(RouterConstants.ARG_COMIC_LOCATION, string.Empty);
        if (!string.IsNullOrEmpty(location))
        {
            ComicModel? comic = await GetComicFromLocation(location);
            if (comic is not null)
            {
                return comic;
            }
        }

        return null;
    }

    //
    // Comic Loader
    //

    public async Task LoadComic(ComicModel comic)
    {
        if (_isLoading)
        {
            _pendingComic = comic;
            return;
        }

        _isLoading = true;
        try
        {
            ComicModel? loadingComic = comic;
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
    }

    public async Task LoadComicInternal(ComicModel comic)
    {
        if (comic == _comic)
        {
            return;
        }

        ViewModel.CloseComicConnection();
        _comic = null;

        if (comic == null)
        {
            ReaderStatusLiveData.Emit(ReaderStatusEnum.Error);
            return;
        }

        _comic = comic;
        ViewModel.SetComic(comic);

        if (!comic.IsExternal)
        {
            await comic.SetCompletionStateToAtLeastStarted();
            HistoryModel.Instance.Add(comic.Id, comic.Title1, true);
        }

        LoadReaderSettings();
        LoadComicInfo();

        if (!comic.IsExternal && !await comic.ReloadImageFiles())
        {
            Log("Failed to load images of '" + comic.Location + "'. ");
            ReaderStatusLiveData.Emit(ReaderStatusEnum.Error);
            return;
        }

        await ViewModel.OpenComicConnection();
        IComicConnection? connection = ViewModel.ComicConnection;
        if (connection is null)
        {
            ReaderStatusLiveData.Emit(ReaderStatusEnum.Error);
            return;
        }

        ReaderStatusLiveData.Emit(ReaderStatusEnum.Loading);

        if (!comic.IsExternal)
        {
            MainReaderView.SetInitialPage(comic.LastPosition);
        }

        var images = new List<IImageSource>();
        for (int i = 0; i < connection.GetImageCount(); ++i)
        {
            images.Add(new ComicImageSource(comic, connection, i));
        }

        MainReaderView.StartLoadingImages(images);

        // Load preview images
        double previewWidth = (double)Application.Current.Resources["ReaderPreviewImageWidth"];
        double previewHeight = (double)Application.Current.Resources["ReaderPreviewImageHeight"];
        ViewModel.PreviewDataSource.Clear();
        for (int i = 0; i < connection.GetImageCount(); ++i)
        {
            ViewModel.PreviewDataSource.Add(new ReaderImagePreviewViewModel
            {
                Image = new SimpleImageView.Model
                {
                    Source = new ComicImageSource(comic, connection, i),
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
    // Reader Settings
    //

    private void LoadReaderSettings()
    {
        ComicModel? comic = _comic;
        if (comic == null)
        {
            return;
        }

        AppSettingsModel.ReaderSettingModel readerSettings = AppSettingsModel.Instance.GetModel().DefaultReaderSetting;
        var readerSettingModel = ReaderSettingDataModel.From(readerSettings, comic);
        GetNavigationPageAbility().SetReaderSettings(readerSettingModel);
        ApplyReaderSettings(readerSettingModel);
    }

    private void ApplyReaderSettings(ReaderSettingDataModel readerSettingModel)
    {
        ReaderView reader = MainReaderView;
        reader.SetIsVertical(readerSettingModel.IsVertical);
        reader.SetIsContinuous(readerSettingModel.IsContinuous);
        reader.SetPageArrangement(readerSettingModel.PageArrangement);
        reader.SetFlowDirection(readerSettingModel.IsLeftToRight);
        reader.SetUseOriginalSize(readerSettingModel.OriginalSize);
        reader.SetPageGap(readerSettingModel.PageGap);
    }

    //
    // Comic Info
    //

    private void LoadComicInfo()
    {
        ComicModel? comic = _comic;
        if (comic == null)
        {
            return;
        }

        CoroutineUtils.Start(async () =>
        {
            _isExternalComicLiveData.Emit(comic.IsExternal);

            if (comic.Title1.Length == 0)
            {
                ViewModel.ComicTitle1 = comic.Title;
            }
            else
            {
                ViewModel.ComicTitle1 = comic.Title1;
                ViewModel.ComicTitle2 = comic.Title2;
            }

            FillRichTextInlines(TbComicDescription.Inlines, comic.Description);
            TbComicDescription.Visibility = TbComicDescription.Inlines.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            ViewModel.ComicDir = comic.Location;
            ViewModel.IsEditable = comic.IsEditable;

            await ViewModel.LoadComicTag();

            bool isFavorite = !comic.IsExternal && FavoriteModel.Instance.FromId(comic.Id) != null;
            SetIsFavorite(isFavorite, false);

            SetCompletionState(comic.CompletionState, false);

            if (!comic.IsExternal)
            {
                ViewModel.Rating = comic.Rating;
            }
        });
    }

    //
    // UI
    //

    private void UpdateReaderUI()
    {
        bool isWorking = ReaderStatusLiveData.GetValue() == ReaderStatusEnum.Working;
        bool previewVisible = isWorking && _gridViewModeEnabled;
        bool readerVisible = isWorking && !previewVisible;

        GGridView.IsHitTestVisible = previewVisible;
        GGridView.Opacity = previewVisible ? 1 : 0;
        GMainSection.Visibility = previewVisible ? Visibility.Collapsed : Visibility.Visible;
        MainReaderView.SetVisibility(readerVisible);
    }

    private void UpdatePage()
    {
        if (PageIndicator == null)
        {
            return;
        }

        ReaderView reader = MainReaderView;
        int currentPage = reader.CurrentPageDisplay;
        PageIndicator.Text = currentPage.ToString() + " / " + reader.PageCount.ToString();
    }

    public void UpdateProgress(ReaderView reader, bool save)
    {
        double page = reader.CurrentPage;
        if (page <= 0.0)
        {
            return;
        }
        int progress;
        if (reader.PageCount <= 0)
        {
            progress = 0;
        }
        else if (reader.IsLastPage)
        {
            progress = 100;
        }
        else
        {
            progress = (int)((float)page / reader.PageCount * 100);
        }
        progress = Math.Min(progress, 100);

        if (save)
        {
            if (_updatingProgress)
            {
                return;
            }
            _updatingProgress = true;
            Task.Run(delegate
            {
                _comic?.SaveProgressAsync(progress, page).Wait();
                _updatingProgress = false;
            });
        }
    }

    //
    // Bottom Tile
    //

    private void OnReaderPointerExited()
    {
        ShowBottomTile();
    }

    private void HideBottomTileDelayed(int delayMilliseconds)
    {
        if (!_bottomTileShowed)
        {
            return;
        }

        if (delayMilliseconds > 0)
        {
            void PostHideTask(int delay)
            {
                CoroutineUtils.Start(async () =>
                {
                    await Task.Delay(delayMilliseconds + 1);

                    if (_bottomTileTargetHideTime == -1)
                    {
                        return;
                    }

                    long currentTick = GetTick();
                    if (currentTick <= _bottomTileTargetHideTime)
                    {
                        PostHideTask((int)(_bottomTileTargetHideTime - currentTick));
                        return;
                    }

                    HideBottomTileDelayed(0);
                });
            }

            _bottomTileTargetHideTime = GetTick() + delayMilliseconds;
            PostHideTask(delayMilliseconds);
            return;
        }

        if (_bottomTileHold || InfoPane.IsPaneOpen || GridViewModeEnabled || GetNavigationPageAbility().GetIsSidePaneOpen())
        {
            return;
        }

        HideBottomTile();
    }

    private void ShowBottomTile()
    {
        _bottomTileTargetHideTime = -1;

        if (_bottomTileShowed)
        {
            return;
        }

        _bottomTileShowed = true;
        GetMainPageAbility().ShowOrHideTitleBar(true);
    }

    private void HideBottomTile()
    {
        _bottomTileTargetHideTime = -1;

        if (!_bottomTileShowed)
        {
            return;
        }

        _bottomTileShowed = false;
        _bottomTileHold = false;
        GetMainPageAbility().ShowOrHideTitleBar(false);
    }

    private void BottomTileSetHold(bool hold)
    {
        _bottomTileHold = hold;

        if (hold)
        {
            ShowBottomTile();
        }
        else
        {
            HideBottomTileDelayed(0);
        }
    }

    //
    // Events
    //

    private void OnGridViewItemClicked(object sender, ItemClickEventArgs e)
    {
        var ctx = (ReaderImagePreviewViewModel)e.ClickedItem;
        GridViewModeEnabled = false;

        ReaderView.ScrollManager.BeginTransaction(MainReaderView, "JumpToGridItem")
            .Page(ctx.Page)
            .Commit();
    }

    private void FavoriteBt_Click(object sender, RoutedEventArgs e)
    {
        SetIsFavorite(!(_isFavorite == true), true);
    }

    private void MarkAsUnreadButton_Click(object sender, RoutedEventArgs e)
    {
        SetCompletionState(ComicCompletionStatusEnum.NotStarted, true);
    }

    private void MarkAsReadingButton_Click(object sender, RoutedEventArgs e)
    {
        SetCompletionState(ComicCompletionStatusEnum.Started, true);
    }

    private void MarkAsFinishedButton_Click(object sender, RoutedEventArgs e)
    {
        SetCompletionState(ComicCompletionStatusEnum.Completed, true);
    }

    private void OnRatingControlValueChanged(RatingControl sender, object args)
    {
        _comic?.SaveRating((int)sender.Value);
    }

    private void OnDirectoryTapped(object sender, TappedRoutedEventArgs e)
    {
        _comic?.ShowInFileExplorer();
    }

    private void OnEditInfoClick(object sender, RoutedEventArgs e)
    {
        ComicModel? comic = _comic;
        if (comic == null)
        {
            return;
        }

        var dialog = new EditComicInfoDialog([comic]);
        _ = dialog.ShowAsync(XamlRoot);
    }

    private void OnNonReaderUIPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        OnReaderPointerExited();
    }

    private void OnReaderPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse || _bottomTileHold)
        {
            return;
        }

        HideBottomTileDelayed(3000);
    }

    private void OnReaderTipCloseButtonClick(InfoBar sender, object args)
    {
        KVDatabase.Default.SetBoolean(DatabaseEntry.KV_LIB_TIPS, KEY_TIP_SHOWN, true);
    }

    private void OnGridViewContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        var item = args.Item as ReaderImagePreviewViewModel;
        var viewHolder = args.ItemContainer.ContentTemplateRoot as ReaderPreviewImage;
        viewHolder?.SetModel(item, args.InRecycleQueue);
    }

    //
    // Utilities
    //

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>()!;
    }

    private INavigationPageAbility GetNavigationPageAbility()
    {
        return GetAbility<INavigationPageAbility>()!;
    }

    public void SetIsFavorite(bool isFavorite, bool writeDatabase)
    {
        if (_isFavorite == isFavorite)
        {
            return;
        }
        _isFavorite = isFavorite;

        FiFavoriteFilled.Visibility = isFavorite ? Visibility.Visible : Visibility.Collapsed;
        FiFavoriteUnfilled.Visibility = isFavorite ? Visibility.Collapsed : Visibility.Visible;

        GetNavigationPageAbility().SetFavorite(isFavorite);

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

    public void SetCompletionState(ComicCompletionStatusEnum completionState, bool writeDatabase)
    {
        if (_completionState == completionState)
        {
            return;
        }
        _completionState = completionState;

        switch (completionState)
        {
            case ComicCompletionStatusEnum.NotStarted:
                SetCompletionStateButton.Icon = new FontIcon
                {
                    Glyph = "\uEA3A"
                };
                SetCompletionStateButton.Label = StringResource.Unread;
                break;
            case ComicCompletionStatusEnum.Started:
                SetCompletionStateButton.Icon = new FontIcon
                {
                    Glyph = "\uED5A"
                };
                SetCompletionStateButton.Label = StringResource.Reading;
                break;
            case ComicCompletionStatusEnum.Completed:
                SetCompletionStateButton.Icon = new FontIcon
                {
                    Glyph = "\uE8FB"
                };
                SetCompletionStateButton.Label = StringResource.Finished;
                break;
            default:
                break;
        }

        MarkAsUnreadButton.Visibility = completionState == ComicCompletionStatusEnum.NotStarted ? Visibility.Collapsed : Visibility.Visible;
        MarkAsReadingButton.Visibility = completionState == ComicCompletionStatusEnum.Started ? Visibility.Collapsed : Visibility.Visible;
        MarkAsFinishedButton.Visibility = completionState == ComicCompletionStatusEnum.Completed ? Visibility.Collapsed : Visibility.Visible;

        ComicModel? comic = _comic;
        if (writeDatabase && comic != null && !comic.IsExternal)
        {
            switch (completionState)
            {
                case ComicCompletionStatusEnum.NotStarted:
                    _ = comic.SetCompletionStateToNotStarted();
                    break;
                case ComicCompletionStatusEnum.Started:
                    _ = comic.SetCompletionStateToStarted();
                    break;
                case ComicCompletionStatusEnum.Completed:
                    _ = comic.SetCompletionStateToCompleted();
                    break;
                default:
                    break;
            }
        }
    }

    private static async Task<ComicModel?> GetComicFromLocation(string location)
    {
        if (File.Exists(location))
        {
            string extension = Path.GetExtension(location);
            if (!AppInfoProvider.IsSupportedExternalFileExtension(extension))
            {
                Logger.E(TAG, $"Unsupported file extension: {extension}");
                return null;
            }

            StorageFile? file = await Storage.TryGetFile(location);
            if (file is null)
            {
                Logger.E(TAG, $"File not found: {location}");
                return null;
            }

            ComicModel? comic = await ComicModel.FromFile(file);
            if (comic is null)
            {
                Logger.E(TAG, $"Failed to create comic from file: {location}");
                return null;
            }

            return comic;
        }

        if (Directory.Exists(location))
        {
            ComicModel? comic = await ComicModel.FromLocation(location, "GetComicFromLocation");
            if (comic is not null)
            {
                return comic;
            }

            string[] filePaths;
            try
            {
                filePaths = Directory.GetFiles(location);
            }
            catch (Exception e)
            {
                Logger.E(TAG, $"Failed to list files in directory: {location}.", e);
                return null;
            }

            List<StorageFile> files = [];
            foreach (string path in filePaths)
            {
                string extension = Path.GetExtension(path);
                if (!AppInfoProvider.IsSupportedImageExtension(extension))
                {
                    continue;
                }

                StorageFile? file = await Storage.TryGetFile(path);
                if (file is null)
                {
                    Logger.E(TAG, $"File not found: {path}");
                    continue;
                }

                files.Add(file);
            }

            if (files.Count == 0)
            {
                Logger.E(TAG, $"No valid image files found in directory: {location}");
                return null;
            }

            comic = ComicModel.FromImageFiles(location, files);
            if (comic is null)
            {
                Logger.E(TAG, $"Failed to create comic from image files in directory: {location}");
                return null;
            }

            return comic;
        }

        Logger.E(TAG, $"Invalid location: {location}");
        return null;
    }

    [GeneratedRegex(REGEX_URL, RegexOptions.None)]
    private static partial Regex URL_REGEX();

    private void FillRichTextInlines(InlineCollection inlines, string richText)
    {
        inlines.Clear();

        Regex urlRegex = URL_REGEX();
        MatchCollection matches = urlRegex.Matches(richText);
        int currentIndex = 0;

        foreach (Match match in matches)
        {
            Uri uri;
            try
            {
                uri = new Uri(match.Value);
            }
            catch (Exception)
            {
                continue;
            }

            int startIndex = match.Index;
            int endIndex = match.Index + match.Length;
            if (endIndex <= currentIndex)
            {
                continue;
            }

            if (startIndex > currentIndex)
            {
                var run = new Run
                {
                    Text = richText[currentIndex..startIndex]
                };
                inlines.Add(run);
            }

            {
                var run = new Run
                {
                    Text = match.Value
                };

                var hyperlink = new Hyperlink
                {
                    NavigateUri = uri
                };

                hyperlink.Inlines.Add(run);
                inlines.Add(hyperlink);
            }

            currentIndex = endIndex;
        }

        if (currentIndex < richText.Length)
        {
            var run = new Run
            {
                Text = richText[currentIndex..]
            };

            inlines.Add(run);
        }
    }

    private static void Log(string message)
    {
        Logger.I("ReaderPage", message);
    }

    private static long GetTick()
    {
        return Environment.TickCount;
    }

    //
    // Classes
    //

    public enum ReaderStatusEnum
    {
        Loading,
        Error,
        Working,
    }
}
