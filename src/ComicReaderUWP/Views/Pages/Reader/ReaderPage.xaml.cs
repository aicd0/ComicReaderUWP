// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.BaseUI.PageAbilities;
using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.SDK.Models;
using ComicReaderUWP.UserControls.Reader;
using ComicReaderUWP.UserControls.Reader.PageLayout;
using ComicReaderUWP.ViewModels;
using ComicReaderUWP.Views.Pages.Main;
using ComicReaderUWP.Views.Pages.SidePane.ComicInfo;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace ComicReaderUWP.Views.Pages.Reader;

internal sealed partial class ReaderPage : BasePage
{
    private const int SAVE_PREOGRESS_INTERVAL = 500;

    // Must be accessed on UI thread
    public static IReadOnlyList<Tuple<int, string>> ActiveTabs { get; private set; } = [];

    //
    // Variables
    //

    private bool _gridViewModeEnabled = false;
    private bool GridViewModeEnabled
    {
        get => _gridViewModeEnabled;
        set
        {
            _gridViewModeEnabled = value;
            _readerNavigationBar.SetGridViewMode(value);
            UpdateReaderUI();

            if (value)
            {
                ReaderImagePreviewViewModel? selectedItem = ViewModel.SelectedPreview;
                if (selectedItem is not null)
                {
                    PreviewGridView.ScrollIntoView(selectedItem);
                }
            }
            else
            {
                FocusReader();
            }
        }
    }

    private ReaderPageViewModel ViewModel { get; set; } = new();
    private bool PointerOnOverlay => !_readerPointerEntered && GetMainWindowAbility().PointerInWindow();

    private readonly ReaderNavigationBar _readerNavigationBar;
    private double _topOverlayHeight = 0.0;
    private double _rightOverlayWidth = 0.0;
    private double _bottomTileHeight = 0.0;
    private bool _displayActive = false;
    private bool _readerPointerEntered = false;
    private bool _bottomTileShowed = false;
    private bool _bottomTileHold = false;
    private long _bottomTileTargetHideTime = -1;
    private long _lastZoomingTicks = 0;
    private int _zoomingStep = 1;

    //
    // Constructor
    //

    public ReaderPage()
    {
        InitializeComponent();
        _readerNavigationBar = new();

        ViewModel.PreviewDataSource = [];
    }

    //
    // Lifecycle
    //

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);
        AddToActiveTabs();
        GetMainPageAbility().SetIcon(new SymbolIconSource { Symbol = Symbol.Pictures });
        GetNavigationPageAbility().SetCustomNavigationBar(_readerNavigationBar);

        // Initialize views
        MainReaderView.OverScrollEnabled = AppSettingsModel.Instance.AutoSwitch;
        _readerNavigationBar.SetWindowId(WindowId);
        ViewModel.SetZooming((int)Math.Round(MainReaderView.Zooming * 100F));

        {
            bool tipShown = AppDB.AppKV.GetCollection(KVNames.KV_LIB_TIPS).GetValueOrDefault(KVNames.KV_KEY_TIPS_READER_TIP_SHOWN, false);
            if (!tipShown)
            {
                ReaderTip.IsOpen = !tipShown;
            }
        }

        {
            bool pinned = AppDB.MainRegistry.CreateKey(RegistryNames.SETTINGS).GetValueOrDefault(RegistryNames.SettingsKey.READER_OVERLAY_PINNED, false);
            ViewModel.SetPinned(pinned);
            UpdatePinUI();
        }

        // Initialize view model
        ViewModel.Initialize(PageActionHandler);
        CoroutineUtils.Run(async () =>
        {
            PlaylistModel playlist = await GetPlaylist(bundle);
            string? serializedPlayback = bundle.GetString(RouterConstants.ARG_PLAYBACK);
            ViewModel.LoadPlaylist(playlist, serializedPlayback);
        });

        ObserveData();
    }

    protected override void OnResume()
    {
        base.OnResume();

        UpdateDisplayStatus();
        AddToActiveTabs();
        SyncCurrentComic();
        GetEventBus().With<PlaybackModel>(EventId.PlaybackChanged).Emit(ViewModel.Playback);
        UpdateReaderUI();
        FocusReader();
    }

    protected override void OnPause()
    {
        base.OnPause();

        UpdateDisplayStatus();
    }

    protected override void OnStop()
    {
        base.OnStop();

        RemoveFromActiveTabs();
        MainReaderView.Destory();
        ViewModel.Destory();
    }

    private void ObserveData()
    {
        AppSettingsModel.Instance.KeepScreenOnBehaviorChangedLiveData.Observe(this, _ =>
        {
            UpdateDisplayStatus();
        });

        GetEventBus().With<double>(EventId.TopOverlayHeight).ObserveSticky(this, h =>
        {
            if (_topOverlayHeight == h)
            {
                return;
            }

            _topOverlayHeight = h;

            Thickness margin = PreviewGridView.Margin;
            margin.Top = h;
            PreviewGridView.Margin = margin;

            if (ViewModel.IsPinned)
            {
                UpdatePinUI();
            }
        });

        GetEventBus().With<double>(EventId.RightOverlayWidth).ObserveSticky(this, w =>
        {
            if (_rightOverlayWidth == w)
            {
                return;
            }

            _rightOverlayWidth = w;

            Thickness margin = PreviewGridView.Margin;
            margin.Right = w;
            PreviewGridView.Margin = margin;
            BottomGrid.Margin = new Thickness(0, 0, w, 0);

            if (ViewModel.IsPinned)
            {
                UpdatePinUI();
            }
        });

        GetEventBus().With<double>(EventId.TitleBarOpacity).ObserveSticky(this, delegate (double opacity)
        {
            BottomGrid.Opacity = opacity;
        });

        GetMainWindowAbility().RegisterMinimizeChangedHandler(this, isMinimized =>
        {
            UpdateDisplayStatus();
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

        GetMainWindowAbility().RegisterFullscreenChangedHandler(this, ViewModel.SetFullscreen);

        ViewModel.TitleLiveData.Observe(this, title =>
        {
            GetMainPageAbility().SetTitle(title);
        }, new ObserveOptions()
        {
            Sticky = true,
            PublishBehavior = LiveDataPublishBehavior.ActiveOnStart,
        });

        ViewModel.PlaybackChangeLiveData.ObserveSticky(this, _ =>
        {
            Route route = Route.Create(GetMainPageAbility().Url)
                .WithParam(RouterConstants.ARG_PLAYBACK, ViewModel.Playback.ToSerializedString());
            GetMainPageAbility().SetUrl(route.Url);
        });

        ViewModel.IsExternalComicLiveData.ObserveSticky(this, _readerNavigationBar.SetExternalComic);

        ViewModel.ReaderStatusLiveData.ObserveSticky(this, info =>
        {
            string readerStatusText = info.Description;
            if (string.IsNullOrEmpty(readerStatusText))
            {
                readerStatusText = info.Status switch
                {
                    ReaderStatusEnum.Loading => StringResourceProvider.Instance.ReaderStatusLoading,
                    ReaderStatusEnum.Error => StringResourceProvider.Instance.ReaderStatusError,
                    _ => string.Empty,
                };
            }

            TbReaderStatus.Text = readerStatusText;
            TbReaderStatus.Visibility = readerStatusText.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateReaderUI();

            if (info.Status == ReaderStatusEnum.Error)
            {
                ShowBottomTile();
            }
        });

        ViewModel.ComicChangedLiveData.ObserveSticky(this, delegate
        {
            SyncCurrentComic();
        });

        ViewModel.IsFavoriteLiveData.ObserveSticky(this, _readerNavigationBar.SetFavorite);

        ViewModel.ReaderLoadingInfoLiveData.ObserveSticky(this, info =>
        {
            MainReaderView.SetConfigurationDatabase(new ReaderConfigDatabase());
            MainReaderView.SetInitialPage(info.InitialPage);
            MainReaderView.StartLoadingImages(info.Images);
            FocusReader();
        });

        _readerNavigationBar.GridViewModeChanged += enabled =>
        {
            GridViewModeEnabled = enabled;
        };

        _readerNavigationBar.InfoPaneExpanded += delegate
        {
            GetMainPageAbility().SetSidePanePage(SidePaneView.PageEnum.ComicInfo);
            GetMainPageAbility().SetSidePaneOpenState(true, force: true);
        };

        _readerNavigationBar.ReaderSettingsChanged += ApplyReaderSettings;

        _readerNavigationBar.FavoriteChanged += isFavorite =>
        {
            ViewModel.SetIsFavorite(isFavorite, true);
        };

        MainReaderView.ReaderEventTapped += sender =>
        {
            BottomTileSetHold(!_bottomTileShowed);
        };

        MainReaderView.ReaderEventPageChanged += (sender, isIntermediate) =>
        {
            ViewModel.SetPageIndex(sender.CurrentPageDiscrete - 1);
            UpdatePage();

            if (!MainReaderView.IsAutoScrolling)
            {
                BottomTileSetHold(false);
            }

            if (!isIntermediate)
            {
                SaveProgress();
                AddToActiveTabs();
                SyncCurrentComic();
            }
        };

        MainReaderView.ReaderEventReaderStateChanged += (sender, state, description) =>
        {
            switch (state)
            {
                case ReaderView.ReaderState.Ready:
                    ViewModel.ReaderStatusLiveData.Emit(new(ReaderStatusEnum.Working, description));
                    UpdatePage();
                    break;
                case ReaderView.ReaderState.Loading:
                    ViewModel.ReaderStatusLiveData.Emit(new(ReaderStatusEnum.Loading, description));
                    break;
                case ReaderView.ReaderState.Error:
                    ViewModel.ReaderStatusLiveData.Emit(new(ReaderStatusEnum.Error, description));
                    break;
            }
        };

        MainReaderView.ReaderEventZoomingChanged += (sender, zooming) =>
        {
            ViewModel.SetZooming((int)Math.Round(zooming * 100F));
        };

        MainReaderView.ReaderEventAutoScrollingChanged += (sender, isAutoScrolling) =>
        {
            if (isAutoScrolling)
            {
                BottomTileSetHold(false);
            }

            ViewModel.IsAutoPlaying = isAutoScrolling;
            UpdateDisplayStatus();
        };

        MainReaderView.ReaderEventOverScroll += (sender, forward) =>
        {
            if (forward)
            {
                ViewModel.Playback.Next();
            }
            else
            {
                ViewModel.Playback.Previous(fromOverScroll: true);
            }
        };
    }

    private async Task<PlaylistModel> GetPlaylist(PageBundle bundle)
    {
        string playlistsRegistry = $"{RegistryNames.TAB_RESOURCES}{GetMainPageAbility().TabId}/Playlists/";

        PlaylistModel? playlist = null;
        string? playlistId = bundle.GetString(RouterConstants.ARG_PLAYLIST_ID);
        if (!string.IsNullOrEmpty(playlistId))
        {
            if (AppDB.MainRegistry.TryGetKey(RegistryNames.PLAYLISTS, out IRegistryKey? key))
            {
                if (key.TryGet(playlistId, out string? serializedPlaylist))
                {
                    playlist = await PlaylistModel.CreateFromSerializedString(serializedPlaylist);
                }
            }

            if (playlist is null && AppDB.MainRegistry.TryGetKey(playlistsRegistry, out key))
            {
                if (key.TryGet(playlistId, out string? serializedPlaylist))
                {
                    playlist = await PlaylistModel.CreateFromSerializedString(serializedPlaylist);
                    if (playlist is not null)
                    {
                        return playlist;
                    }
                }
            }
        }
        else
        {
            playlistId = Guid.NewGuid().ToString();
            Route route = Route.Create(GetMainPageAbility().Url)
                .WithParam(RouterConstants.ARG_PLAYLIST_ID, playlistId);
            GetMainPageAbility().SetUrl(route.Url);
        }

        playlist ??= PlaylistModel.CreateEmpty();
        AppDB.MainRegistry.CreateKey(playlistsRegistry).Set(playlistId, playlist.ToSerializedString());
        return playlist;
    }

    //
    // Progress
    //

    private bool _savingProgress = false;
    private bool _saveProgressInvalidated = false;

    public void SaveProgress()
    {
        if (_savingProgress)
        {
            _saveProgressInvalidated = true;
            return;
        }

        CoroutineUtils.Run(async () =>
        {
            _savingProgress = true;
            try
            {
                do
                {
                    _saveProgressInvalidated = false;

                    ComicModel? comic = ViewModel.Comic;
                    if (comic is null)
                    {
                        continue;
                    }

                    ReaderView reader = MainReaderView;
                    double page = Math.Max(0, reader.CurrentPage);
                    int progress = reader.CurrentPagePercentage;
                    await comic.SetProgress(progress, page);
                    await Task.Delay(SAVE_PREOGRESS_INTERVAL);
                }
                while (_saveProgressInvalidated);
            }
            finally
            {
                _savingProgress = false;
            }
        });
    }

    //
    // Reader
    //

    private void ApplyReaderSettings(ReaderSettingsModel readerSettingModel)
    {
        ReaderView reader = MainReaderView;

        reader.SetIsVertical(readerSettingModel.IsVertical);
        reader.SetIsContinuous(readerSettingModel.IsContinuous);
        reader.SetFlowDirection(readerSettingModel.IsLeftToRight);
        reader.SetUseOriginalSize(readerSettingModel.OriginalSize);
        reader.SetAutoScrollSpeed(readerSettingModel.AutoScrollSpeed);
        reader.SetPageGap(readerSettingModel.PageGap);
        reader.SetImageRotation(readerSettingModel.ImageRotation);
        reader.SetImageFlip(readerSettingModel.ImageFlip);
        reader.SetImageInvert(readerSettingModel.ImageInvert);
        reader.SetAntiAliasingFilter(readerSettingModel.AntiAliasingFilter);

        reader.SetPageLayoutManager(new SimplePageLayoutManager()
        {
            TwoPageMode = readerSettingModel.PageLayout.TwoPageMode,
            EnableCover = readerSettingModel.PageLayout.EnableCover,
            RightToLeft = readerSettingModel.IsLeftToRight == readerSettingModel.PageLayout.SwapLeftAndRightPages,
            SpreadDetection = readerSettingModel.PageLayout.SpreadDetection,
        });

        PlaybackSlider.FlowDirection = readerSettingModel.IsLeftToRight || readerSettingModel.IsVertical ?
            FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        ViewModel.IsAutoPlayEnabled = readerSettingModel.AutoScrollSpeed > 0;
    }

    private void UpdateReaderUI()
    {
        bool isWorking = ViewModel.ReaderStatus == ReaderStatusEnum.Working;
        bool previewVisible = isWorking && _gridViewModeEnabled;

        // Setting Visibility.Collapsed here prevents GridView from loading eagerly
        PreviewGridView.Opacity = previewVisible ? 1.0 : 0.0;
        PreviewGridView.IsHitTestVisible = previewVisible;

        // Setting Visibility.Collapsed here prevents ReaderView from locating target page offset
        GMainSection.Opacity = previewVisible ? 0.0 : 1.0;
        GMainSection.IsHitTestVisible = !previewVisible;
    }

    private void FocusReader()
    {
        GetMainPageAbility().SetSidePaneOpenState(false, force: false); // Remove focus on sidebar

        UIElement element = MainReaderView;
        void PostFocus(int round)
        {
            CoroutineUtils.PostInMainThreadAsync(async () =>
            {
                await Task.Delay(1);
                if (!element.IsHitTestVisible || element.Visibility != Visibility.Visible || !GetMainWindowAbility().IsActive)
                {
                    return;
                }

                round++;
                element.Focus(FocusState.Programmatic);

                if (round >= 10)
                {
                    return;
                }

                PostFocus(round);
            }, Microsoft.UI.Dispatching.DispatcherQueuePriority.Low);
        }

        PostFocus(0);
    }

    private void UpdateDisplayStatus()
    {
        bool minimized = GetMainWindowAbility().IsMinimized;
        AppSettingsModel.KeepScreenOnBehaviorEnum behavior = AppSettingsModel.Instance.KeepScreenOnBehavior;
        bool active = !minimized && IsResumed && behavior switch
        {
            AppSettingsModel.KeepScreenOnBehaviorEnum.Never => false,
            AppSettingsModel.KeepScreenOnBehaviorEnum.Always => true,
            AppSettingsModel.KeepScreenOnBehaviorEnum.DuringAutoScrolling => MainReaderView.IsAutoScrolling,
            _ => false
        };

        if (active == _displayActive)
        {
            return;
        }

        _displayActive = active;
        if (active)
        {
            DisplayRequestManager.IncrememtKeepScreenOn();
        }
        else
        {
            DisplayRequestManager.DecrememtKeepScreenOn();
        }
    }

    //
    // Bottom Tile
    //

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
                CoroutineUtils.Run(async () =>
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

        if (ViewModel.IsPinned || _bottomTileHold || GridViewModeEnabled || PointerOnOverlay)
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
    // Playback control
    //

    private void PlaybackSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        ReaderView reader = MainReaderView;
        double currentValue = reader.CurrentPageDiscrete;
        double newValue = e.NewValue;
        if (Math.Abs(currentValue - newValue) < 0.5)
        {
            return;
        }

        reader.SetCurrentPage(newValue);
    }

    private void PlaybackPlayButton_Click(object sender, RoutedEventArgs e)
    {
        MainReaderView.IsAutoScrolling = !MainReaderView.IsAutoScrolling;
    }

    private void PlaybackPreviousButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Playback.Previous(fromOverScroll: false);
    }

    private void PlaybackNextButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Playback.Next();
    }

    private void PlaybackPlaylistButton_Click(object sender, RoutedEventArgs e)
    {
        GetMainPageAbility().SetSidePanePage(SidePaneView.PageEnum.Playlist);
        GetMainPageAbility().SetSidePaneOpenState(true, force: true);
    }

    private void PlaybackMoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe)
        {
            return;
        }

        ComicModel? comic = ViewModel.Comic;
        if (comic is null)
        {
            return;
        }

        List<BaseMenuFlyoutItemModel> menuItems = CreatePlaybackMoreMenuItems();
        if (menuItems.Count == 0)
        {
            return;
        }

        MenuFlyout flyout = new()
        {
            Placement = FlyoutPlacementMode.Top,
        };
        foreach (BaseMenuFlyoutItemModel item in menuItems)
        {
            flyout.Items.Add(item.CreateMenuFlyoutItem());
        }

        flyout.ShowAt(fe);
    }

    private List<BaseMenuFlyoutItemModel> CreatePlaybackMoreMenuItems()
    {
        List<BaseMenuFlyoutItemModel> items = [];

        {
            bool repeat = ViewModel.Playback.IsRepeat;
            items.Add(new ToggleMenuFlyoutItemModel()
            {
                Text = StringResourceProvider.Instance.Repeat,
                IsChecked = repeat,
                Click = () =>
                {
                    ViewModel.Playback.IsRepeat = !repeat;
                },
            });
        }

        {
            bool shuffle = ViewModel.Playback.IsShuffle;
            items.Add(new ToggleMenuFlyoutItemModel()
            {
                Text = StringResourceProvider.Instance.Shuffle,
                IsChecked = shuffle,
                Click = () =>
                {
                    ViewModel.Playback.IsShuffle = !shuffle;
                },
            });
        }

        items.Add(new SeparatorMenuFlyoutItemModel());

        {
            bool autoSwitch = MainReaderView.OverScrollEnabled;
            items.Add(new ToggleMenuFlyoutItemModel()
            {
                Text = StringResourceProvider.Instance.AutoSwitch,
                IsChecked = autoSwitch,
                Click = () =>
                {
                    AppSettingsModel.Instance.AutoSwitch = !autoSwitch;
                    MainReaderView.OverScrollEnabled = !autoSwitch;
                },
            });
        }

        return items;
    }

    private void UpdatePage()
    {
        ReaderView reader = MainReaderView;
        int totalPages = Math.Max(0, reader.PageCount);
        int currentPage = reader.CurrentPageDiscrete;
        int percentage = reader.CurrentPagePercentage;
        ViewModel.PrimaryPageIndicatorText = $"{currentPage} / {totalPages}";
        ViewModel.SecondaryPageIndicatorText = $"{percentage}%";

        // Use different order to prevent unwanted change events
        if (PlaybackSlider.Maximum > currentPage)
        {
            PlaybackSlider.Value = currentPage;
            PlaybackSlider.Maximum = totalPages;
        }
        else
        {
            PlaybackSlider.Maximum = totalPages;
            PlaybackSlider.Value = currentPage;
        }
    }

    //
    // Pin
    //

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SetPinned(!ViewModel.IsPinned);
        UpdatePinUI();
        AppDB.MainRegistry.CreateKey(RegistryNames.SETTINGS).Set(RegistryNames.SettingsKey.READER_OVERLAY_PINNED, ViewModel.IsPinned);
    }

    private void UpdatePinUI()
    {
        if (ViewModel.IsPinned)
        {
            MainReaderView.Margin = new Thickness(0, _topOverlayHeight, _rightOverlayWidth, _bottomTileHeight);
            ShowBottomTile();
        }
        else
        {
            MainReaderView.Margin = new Thickness(0);
        }
    }

    //
    // Events
    //

    private void BottomGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_bottomTileHeight == e.NewSize.Height)
        {
            return;
        }

        _bottomTileHeight = e.NewSize.Height;

        if (ViewModel.IsPinned)
        {
            UpdatePinUI();
        }
    }

    private void Zooming_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        PointerPoint pt = e.GetCurrentPoint(null);
        int delta = pt.Properties.MouseWheelDelta / (int)Windows.Win32.PInvoke.WHEEL_DELTA;
        if (delta == 0)
        {
            return;
        }

        long tick = GetTick();
        long interval = tick - _lastZoomingTicks;
        _lastZoomingTicks = tick;

        if (interval < 100)
        {
            _zoomingStep = Math.Min(_zoomingStep * 2, 25);
        }
        else if (interval > 300)
        {
            _zoomingStep = 1;
        }

        MainReaderView.Zooming += delta * _zoomingStep * 0.01F;
    }

    private void FullscreenButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetMainWindowAbility().IsFullscreen)
        {
            GetMainWindowAbility().ExitFullscreen();
        }
        else
        {
            GetMainWindowAbility().EnterFullscreen();
        }
    }

    private void OnGridViewItemClicked(object sender, ItemClickEventArgs e)
    {
        var ctx = (ReaderImagePreviewViewModel)e.ClickedItem;
        GridViewModeEnabled = false;
        MainReaderView.SetCurrentPage(ctx.Page);
    }

    private void OnReaderPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_readerPointerEntered)
        {
            return;
        }

        _readerPointerEntered = false;

        // Post detection to allow routed event to be dispatched to root
        CoroutineUtils.PostInMainThread(() =>
        {
            if (PointerOnOverlay)
            {
                ShowBottomTile();
            }
        });
    }

    private void OnReaderPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _readerPointerEntered = true;
        if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse || _bottomTileHold)
        {
            return;
        }

        HideBottomTileDelayed(1000);
    }

    private void OnReaderTipCloseButtonClick(InfoBar sender, object args)
    {
        AppDB.AppKV.GetCollection(KVNames.KV_LIB_TIPS).Set(KVNames.KV_KEY_TIPS_READER_TIP_SHOWN, true);
    }

    private void OnGridViewContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        var item = args.Item as ReaderImagePreviewViewModel;
        var viewHolder = args.ItemContainer.ContentTemplateRoot as ReaderPreviewImage;
        viewHolder?.SetModel(item, args.InRecycleQueue);
    }

    private void PageIndicator_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        PointerPoint pt = e.GetCurrentPoint(null);
        int delta = -pt.Properties.MouseWheelDelta / (int)Windows.Win32.PInvoke.WHEEL_DELTA;
        MainReaderView.MoveFrame(delta);
    }

    private void Reader_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        if (sender is not FrameworkElement fe)
        {
            return;
        }

        ComicModel? comic = ViewModel.Comic;
        if (comic is null)
        {
            return;
        }

        args.Handled = true;

        CoroutineUtils.Run(async () =>
        {
            List<BaseMenuFlyoutItemModel> menuItems = await MenuFlyoutItemsCreator.CreateComicMenuItems(PageActionHandler, comic, ViewModel.Playlist.ToBuilder());

            var flyout = new MenuFlyout();
            foreach (BaseMenuFlyoutItemModel item in menuItems)
            {
                flyout.Items.Add(item.CreateMenuFlyoutItem());
            }

            if (flyout is null)
            {
                return;
            }

            if (args.TryGetPosition(fe, out Windows.Foundation.Point point))
            {
                flyout.ShowAt(fe, new FlyoutShowOptions { Position = point });
            }
            else
            {
                flyout.ShowAt(fe);
            }
        });
    }

    //
    // Utilities
    //

    private IMainWindowAbility GetMainWindowAbility()
    {
        return GetAbility<IMainWindowAbility>()!;
    }

    private IMainPageAbilityForTab GetMainPageAbility()
    {
        return GetAbility<IMainPageAbilityForTab>()!;
    }

    private INavigationPageAbility GetNavigationPageAbility()
    {
        return GetAbility<INavigationPageAbility>()!;
    }

    private void SyncCurrentComic()
    {
        ComicModel? comic = ViewModel.Comic;

        if (comic is not null)
        {
            _readerNavigationBar.SetReaderSettings(comic);
        }

        ComicChangedEventArgs args = new()
        {
            Comic = comic,
            Playlist = ViewModel.Playlist,
            PageIndices = GetPageIndicesFromPage(MainReaderView.CurrentPage, MainReaderView.PageCount),
        };
        GetEventBus().With<ComicChangedEventArgs>(EventId.ComicInfoChanged).Emit(args);
    }

    private void AddToActiveTabs()
    {
        if (!IsStarted)
        {
            return;
        }

        int windowId = WindowId;
        string tabId = GetMainPageAbility().TabId;

        if (ActiveTabs.Count > 0)
        {
            Tuple<int, string> tab = ActiveTabs[ActiveTabs.Count - 1];
            if (tab.Item1 == windowId && tab.Item2 == tabId)
            {
                return;
            }
        }

        List<Tuple<int, string>> copy = [.. ActiveTabs];
        for (int i = copy.Count - 1; i >= 0; i--)
        {
            Tuple<int, string> tab = copy[i];
            if (tab.Item1 == windowId && tab.Item2 == tabId)
            {
                copy.RemoveAt(i);
            }
        }

        copy.Add(new(windowId, tabId));
        ActiveTabs = copy;
    }

    private void RemoveFromActiveTabs()
    {
        int windowId = WindowId;
        string tabId = GetMainPageAbility().TabId;

        List<Tuple<int, string>> copy = [.. ActiveTabs];
        for (int i = copy.Count - 1; i >= 0; i--)
        {
            Tuple<int, string> tab = copy[i];
            if (tab.Item1 == windowId && tab.Item2 == tabId)
            {
                copy.RemoveAt(i);
            }
        }

        ActiveTabs = copy;
    }

    private static HashSet<int> GetPageIndicesFromPage(double page, int pageCount)
    {
        HashSet<int> indices = [];
        int floor = (int)Math.Floor(page);
        int ceiling = (int)Math.Ceiling(page);

        if (floor > 0 && floor <= pageCount && page - floor <= 0.75)
        {
            indices.Add(floor - 1);
        }

        if (ceiling != floor && ceiling > 0 && ceiling <= pageCount && ceiling - page <= 0.75)
        {
            indices.Add(ceiling - 1);
        }

        return indices;
    }

    private static long GetTick()
    {
        return Environment.TickCount;
    }

    //
    // Types
    //

    public enum ReaderStatusEnum
    {
        Loading,
        Error,
        Working,
    }

    public class ReaderStatusInfo(ReaderStatusEnum status, string description = "")
    {
        public ReaderStatusEnum Status { get; } = status;
        public string Description { get; } = description;
    }

    private class ReaderConfigDatabase : ReaderView.IConfigurationDatabase
    {
        public string? ReadConfiguration(string key)
        {
            return AppDB.AppKV.GetCollection(KVNames.KV_LIB_READER_STATE).GetValue<string>(key);
        }

        public void WriteConfiguration(string key, string value)
        {
            AppDB.AppKV.GetCollection(KVNames.KV_LIB_READER_STATE).Set(key, value);
        }
    }
}
