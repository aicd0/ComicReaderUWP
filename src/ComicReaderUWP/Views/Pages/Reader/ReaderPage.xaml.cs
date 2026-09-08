// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.BaseUI.PageAbilities;
using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Plugins;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Data.Models.Playback;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.SDK.Models;
using ComicReaderUWP.UserControls.Reader;
using ComicReaderUWP.UserControls.Reader.PageLayout;
using ComicReaderUWP.Views.Pages.Main.Sidebar;
using ComicReaderUWP.Views.Pages.Sidebar.ComicInfo;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace ComicReaderUWP.Views.Pages.Reader;

internal sealed partial class ReaderPage : BasePage
{
    private const int SAVE_PREOGRESS_INTERVAL = 500;

    // Can only be accessed by UI thread
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
                foreach (ReaderPreviewImageViewModel item in ViewModel.SelectedPreviews)
                {
                    PreviewGridView.ScrollIntoView(item);
                }
            }
            else
            {
                FocusReader();
            }
        }
    }

    private ReaderPageViewModel ViewModel { get; set; } = new();

    private readonly ReaderNavigationBar _readerNavigationBar;
    private bool _displayActive = false;
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
        GetMainPageAbility().SetCustomCenteredNavigationBar(_readerNavigationBar);

        // Initialize views
        MainReaderView.OverScrollEnabled = AppSettingsModel.Instance.AutoSwitch;
        _readerNavigationBar.Initialize(GetMainWindowAbility());
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
            UpdatePinRelatedUI();
        }

        // Initialize view model
        double scale = GetMainWindowAbility().GetRasterizationScale();
        double previewImageWidth = (double)Application.Current.Resources["ReaderPreviewImageWidth"] * scale;
        double previewImageHeight = (double)Application.Current.Resources["ReaderPreviewImageHeight"] * scale;
        ViewModel.Initialize(PageActionHandler, previewImageWidth, previewImageHeight);

        CoroutineUtils.Run(async () =>
        {
            PlaylistModel playlist = await GetPlaylist(bundle);
            string? serializedPlayback = bundle.GetString(RouterConstants.ARG_PLAYBACK);

            string? pageArg = bundle.GetString(RouterConstants.ARG_PAGE);
            if (string.IsNullOrEmpty(pageArg) ||
                !double.TryParse(pageArg, CultureInfo.InvariantCulture, out double page) ||
                !double.IsFinite(page) || page < 0.0)
            {
                page = -1.0;
            }

            ViewModel.LoadPlaylist(playlist, serializedPlayback, page);
        });

        ObserveData();
    }

    protected override void OnResume()
    {
        base.OnResume();

        UpdateDisplayStatus();
        AddToActiveTabs();
        SyncCurrentComic();
        GetWindowEventBus().With<PlaybackModel>(EventId.PlaybackChanged).Emit(ViewModel.Playback);
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
        GlobalEvent.Instance.FavoriteUpdated.Observe(this, _ =>
        {
            ViewModel.UpdateFavoriteStatus();
        });

        AppSettingsModel.Instance.KeepScreenOnBehaviorChangedLiveData.Observe(this, _ =>
        {
            UpdateDisplayStatus();
        });

        GetWindowEventBus().With<double>(EventId.TopOverlayHeight).ObserveSticky(this, h =>
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
                UpdatePinRelatedUI();
            }
        });

        GetWindowEventBus().With<double>(EventId.RightOverlayWidth).ObserveSticky(this, w =>
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
                UpdatePinRelatedUI();
            }
        });

        GetWindowEventBus().With<double>(EventId.TitleBarOpacity).ObserveSticky(this, opacity =>
        {
            BottomGrid.Opacity = opacity;

            bool autoToggleOverlaysOnCursor = AppSettingsModel.Instance.AutoToggleOverlaysOnCursor;
            BottomGrid.IsHitTestVisible = autoToggleOverlaysOnCursor || opacity >= 0.5;
            GetMainPageAbility().SetHiddenOverlayHitTestVisibility(autoToggleOverlaysOnCursor);
        });

        GetMainWindowAbility().RegisterMinimizeChangedHandler(this, isMinimized =>
        {
            UpdateDisplayStatus();
        });

        GetMainWindowAbility().RegisterFullscreenChangedHandler(this, ViewModel.SetFullscreen);

        GetMainPageAbility().RegisterRefreshHandler(this, ViewModel.Playback.Refresh);

        GetMainPageAbility().RegisterOverlayVisibilityChangedHandler(this, visible =>
        {
            if (visible)
            {
                ShowOverlay();
            }
            else
            {
                HideOverlay();
            }
        });

        GetMainWindowAbility().RegisterPointerInsideRootElementChangedHandler(this, isInside =>
        {
            _isPointerInsideRootElement = isInside;
            UpdateOverlayState();
        });

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
            StringBuilder textSb = new();
            switch (info.Status)
            {
                case ReaderStatusEnum.Loading:
                    textSb.Append(StringResourceProvider.Instance.ReaderStatusLoading);

                    if (info.Progress >= 0)
                    {
                        int progress = Math.Clamp(info.Progress, 0, 100);
                        textSb.Append($" ({progress}%)");
                    }

                    if (!string.IsNullOrEmpty(info.Description))
                    {
                        textSb.AppendLine().Append(info.Description);
                    }

                    break;
                case ReaderStatusEnum.Error:
                    textSb.Append(StringResourceProvider.Instance.ReaderStatusError);

                    if (!string.IsNullOrEmpty(info.Description))
                    {
                        textSb.AppendLine().Append(info.Description);
                    }

                    break;
                case ReaderStatusEnum.Working:
                    break;
                default:
                    break;
            }

            string readerStatusText = textSb.ToString();
            TbReaderStatus.Text = readerStatusText;
            TbReaderStatus.Visibility = readerStatusText.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateReaderUI();

            if (info.Status == ReaderStatusEnum.Error)
            {
                ShowOverlay();
            }
        });

        ViewModel.ComicChangedLiveData.ObserveSticky(this, _ =>
        {
            SyncCurrentComic();
        });

        ViewModel.IsFavoriteLiveData.ObserveSticky(this, _readerNavigationBar.SetFavorite);

        ViewModel.ReaderLoadingInfoLiveData.ObserveSticky(this, info =>
        {
            MainReaderView.SetConfigurationDatabase(new ReaderConfigDatabase());
            MainReaderView.SetInitialPage(info.InitialPage);
            MainReaderView.StartLoadingImages(info.Images);
        });

        _readerNavigationBar.GridViewModeChanged += enabled =>
        {
            GridViewModeEnabled = enabled;
        };

        _readerNavigationBar.InfoPaneExpanded += delegate
        {
            GetMainPageAbility().SetSidePanePage(SidebarView.ITEM_COMIC_INFO);
            GetMainPageAbility().SetSidePaneOpenState(true, force: true);
        };

        _readerNavigationBar.ReaderSettingsChanged += ApplyReaderSettings;

        _readerNavigationBar.FavoriteChanged += isFavorite =>
        {
            ViewModel.SetIsFavorite(isFavorite, true);
        };

        MainReaderView.ReaderEventTapped += sender =>
        {
            BottomTileSetHold(!_isOverlayVisible);
        };

        MainReaderView.ReaderEventPageChanged += (sender, isIntermediate) =>
        {
            HashSet<int> pageIndices = GetCurrentPageIndices();
            ViewModel.SetPageIndices(pageIndices);
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

        MainReaderView.ReaderEventReaderStateChanged += (sender, state, progress) =>
        {
            switch (state)
            {
                case ReaderView.ReaderStatus.Ready:
                    ViewModel.ReaderStatusLiveData.Emit(new()
                    {
                        Status = ReaderStatusEnum.Working,
                        Progress = progress,
                    });
                    UpdatePage();
                    FocusReader();
                    break;
                case ReaderView.ReaderStatus.Loading:
                    ViewModel.ReaderStatusLiveData.Emit(new()
                    {
                        Status = ReaderStatusEnum.Loading,
                        Progress = progress,
                    });
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
            if (ViewModel.ReaderStatus != ReaderStatusEnum.Working)
            {
                return;
            }

            if (forward)
            {
                ViewModel.Playback.Next();
            }
            else
            {
                ViewModel.Playback.Previous(fromOverScroll: true);
            }
        };

        MainReaderView.ImageContextRequested = async (sender, args) =>
        {
            ComicModel? comic = ViewModel.Comic;
            if (comic is null)
            {
                return [];
            }

            IReadOnlyList<BaseMenuFlyoutItemModel> imageItems = await ViewModel.CreateImageContextMenuItems(args.ImageIndex, args.Image);
            IReadOnlyList<BaseMenuFlyoutItemModel> comicItems = await ViewModel.CreateComicContextMenuItems();

            if (imageItems.Count == 0)
            {
                return comicItems;
            }

            string TrimText(string text)
            {
                const int maxLength = 30;

                if (text.Length <= maxLength)
                {
                    return text;
                }

                int count = maxLength / 2 - 1;
                return $"{text[..count]} ... {text[^count..]}";
            }

            List<BaseMenuFlyoutItemModel> items = [.. imageItems];

            if (comicItems.Count > 0)
            {
                items.Add(new SeparatorMenuFlyoutItemModel());
                items.Add(new SubItemMenuFlyoutItemModel()
                {
                    Text = TrimText(comic.Title),
                    Icon = new FontIconSource() { Glyph = "\uE7AA" },
                    Items = comicItems,
                });
            }

            return items;
        };
    }

    private async Task<PlaylistModel> GetPlaylist(PageBundle bundle)
    {
        string playlistsRegistry = $"{RegistryNames.TAB_RESOURCES}/{GetMainPageAbility().TabId}/Playlists";

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
    // Common Input Events
    //

    private void ReaderGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isPointerInsideReader = true;
        UpdateOverlayState();
    }

    private void ReaderGrid_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isPointerInsideReader = false;
        UpdateOverlayState();
    }

    private void Reader_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        if (sender is not FrameworkElement fe)
        {
            return;
        }

        args.Handled = true;

        CoroutineUtils.Run(async () =>
        {
            IReadOnlyList<BaseMenuFlyoutItemModel> menuItems = await ViewModel.CreateComicContextMenuItems();

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

    private void PageIndicator_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        PointerPoint pt = e.GetCurrentPoint(null);
        int delta = -pt.Properties.MouseWheelDelta / (int)Windows.Win32.PInvoke.WHEEL_DELTA;
        MainReaderView.MoveFrame(delta);
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SetPinned(!ViewModel.IsPinned);

        UpdatePinRelatedUI();

        AppDB.MainRegistry.CreateKey(RegistryNames.SETTINGS).Set(RegistryNames.SettingsKey.READER_OVERLAY_PINNED, ViewModel.IsPinned);
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

    private void ReaderTipCloseButton_Click(InfoBar sender, object args)
    {
        AppDB.AppKV.GetCollection(KVNames.KV_LIB_TIPS).Set(KVNames.KV_KEY_TIPS_READER_TIP_SHOWN, true);
    }

    //
    // BottomGrid
    //

    private double _bottomGridHeight = 0.0;

    private void BottomGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_bottomGridHeight == e.NewSize.Height)
        {
            return;
        }

        _bottomGridHeight = e.NewSize.Height;

        if (ViewModel.IsPinned)
        {
            UpdatePinRelatedUI();
        }
    }

    //
    // Progress
    //

    private bool _savingProgress = false;
    private bool _saveProgressInvalidated = false;

    public void SaveProgress()
    {
        int CalculatePercentage(ReaderView reader)
        {
            double minimumPage = 0.5;
            double maximumPage = reader.PageCount;
            double page = Math.Clamp(reader.CurrentPage, minimumPage, maximumPage);
            return Math.Clamp((int)Math.Round(100.0 * (page - minimumPage) / (maximumPage - minimumPage)), 0, 100);
        }

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
                    int progress = CalculatePercentage(reader);
                    double page = Math.Max(0, reader.CurrentPage);
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
        reader.SetPageSpacing(readerSettingModel.PageSpacing);
        reader.SetImageRotation(readerSettingModel.ImageRotation);
        reader.SetImageFlip(readerSettingModel.ImageFlip);
        reader.SetAntiAliasingFilter(readerSettingModel.AntiAliasingFilterPercentage * 0.01);
        reader.SetImageBrightness((readerSettingModel.BrightnessPercentage - 50) * 0.02F);
        reader.SetImageContrast((readerSettingModel.ContrastPercentage - 50) * 0.02F);
        reader.SetImageSaturation(readerSettingModel.SaturationPercentage * 0.01F);
        reader.SetImageInvert(readerSettingModel.ImageInvert);

        reader.SetPageLayoutManager(new SimplePageLayoutManager()
        {
            TwoPageMode = readerSettingModel.PageLayout.TwoPageMode,
            CoverPageCount = readerSettingModel.PageLayout.CoverPageCount,
            RightToLeft = readerSettingModel.IsLeftToRight == readerSettingModel.PageLayout.SwapLeftAndRightPages,
            SpreadDetection = readerSettingModel.PageLayout.SpreadDetection,
        });

        FlowDirection preferredFlowDirection = readerSettingModel.IsLeftToRight ?
            FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        ViewModel.PreferredFlowDirection = preferredFlowDirection;
        PreviewGridView.ItemsPanelRoot.FlowDirection = preferredFlowDirection;

        ViewModel.IsAutoPlayEnabled = readerSettingModel.AutoScrollSpeed > 0;
    }

    private void UpdateReaderUI()
    {
        bool previewVisible = _gridViewModeEnabled;

        // Setting Visibility.Collapsed here prevents GridView from loading eagerly
        PreviewGridView.Opacity = previewVisible ? 1.0 : 0.0;
        PreviewGridView.IsHitTestVisible = previewVisible;

        // Setting Visibility.Collapsed here prevents ReaderView from locating target page offset
        GMainSection.Opacity = previewVisible ? 0.0 : 1.0;
        GMainSection.IsHitTestVisible = !previewVisible;
    }

    private void FocusReader()
    {
        IMainWindowAbility windowAbility = GetMainWindowAbility();
        if (windowAbility.IsFocusLocked)
        {
            return;
        }

        GetMainPageAbility().SetSidePaneOpenState(false, force: false); // Remove focus on sidebar

        UIElement element = MainReaderView;
        void PostFocus(int round)
        {
            CoroutineUtils.PostInMainThreadAsync(async () =>
            {
                await Task.Delay(1);
                if (!element.IsHitTestVisible || element.Visibility != Visibility.Visible || !windowAbility.IsActive)
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
    // Overlay
    //

    private bool _shouldOverlayVisible = false;
    private bool _isPointerInsideRootElement = true;
    private bool _isPointerInsideReader = false;

    private double _topOverlayHeight = 0.0;
    private double _rightOverlayWidth = 0.0;
    private bool _isOverlayVisible = false;
    private bool _isOverlayHold = false;
    private long _hideOverlayDeadline = -1;

    private void UpdateOverlayState()
    {
        bool GetPointerInsideWindowState()
        {
            SizeF windowSize = GetMainWindowAbility().WindowSize;
            PointF pointerPos = GetMainWindowAbility().GetPointerPosition();
            const float padding = 10.0F;
            return
                pointerPos.X >= padding &&
                pointerPos.X <= windowSize.Width - padding &&
                pointerPos.Y >= padding &&
                pointerPos.Y <= windowSize.Height - padding;
        }

        bool isPointerInsideWindow = GetPointerInsideWindowState();
        bool shouldOverlayVisible = (isPointerInsideWindow || _isPointerInsideRootElement) && !_isPointerInsideReader;
        if (shouldOverlayVisible == _shouldOverlayVisible)
        {
            return;
        }

        _shouldOverlayVisible = shouldOverlayVisible;

        if (AppSettingsModel.Instance.AutoToggleOverlaysOnCursor)
        {
            if (shouldOverlayVisible)
            {
                ShowOverlay();
            }
            else
            {
                TryHideOverlay(1000);
            }
        }
    }

    private void TryHideOverlay(int delayMilliseconds = 0)
    {
        if (!_isOverlayVisible ||
            ViewModel.IsPinned ||
            _isOverlayHold ||
            _shouldOverlayVisible ||
            GridViewModeEnabled)
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

                    if (_hideOverlayDeadline == -1)
                    {
                        return;
                    }

                    long currentTick = GetTick();
                    if (currentTick <= _hideOverlayDeadline)
                    {
                        PostHideTask((int)(_hideOverlayDeadline - currentTick));
                        return;
                    }

                    TryHideOverlay();
                });
            }

            _hideOverlayDeadline = GetTick() + delayMilliseconds;
            PostHideTask(delayMilliseconds);
            return;
        }

        HideOverlay();
    }

    private void ShowOverlay()
    {
        _hideOverlayDeadline = -1;

        if (_isOverlayVisible)
        {
            return;
        }

        _isOverlayVisible = true;
        GetMainPageAbility().SetOverlayVisibility(true);
    }

    private void HideOverlay()
    {
        _hideOverlayDeadline = -1;

        if (!_isOverlayVisible)
        {
            return;
        }

        _isOverlayVisible = false;
        _isOverlayHold = false;
        GetMainPageAbility().SetOverlayVisibility(false);
    }

    private void BottomTileSetHold(bool hold)
    {
        _isOverlayHold = hold;

        if (hold)
        {
            ShowOverlay();
        }
        else
        {
            TryHideOverlay();
        }
    }

    //
    // Playback control
    //

    private void PlaybackSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        ReaderView reader = MainReaderView;

        double currentValue = reader.CurrentPage;
        double newValue = e.NewValue;
        if (Math.Abs(currentValue - newValue) < 0.01)
        {
            return;
        }

        reader.SetPage(newValue);
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
        GetMainPageAbility().SetSidePanePage(SidebarView.ITEM_PLAYLIST);
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
        int CalculatePercentage(ReaderView reader)
        {
            double minimumPage = 0.5;
            double maximumPage = reader.PageCount + 0.5;
            double page = Math.Clamp(reader.CurrentPage, minimumPage, maximumPage);
            return Math.Clamp((int)Math.Round(100.0 * (page - minimumPage) / (maximumPage - minimumPage)), 0, 100);
        }

        ReaderView reader = MainReaderView;

        int totalPages = Math.Max(0, reader.PageCount);
        double currentPage = reader.CurrentPage;
        int percentage = CalculatePercentage(reader);

        ViewModel.PrimaryPageIndicatorText = $"{currentPage:0.#} / {totalPages}";
        ViewModel.SecondaryPageIndicatorText = $"{percentage}%";

        // Use different order to prevent unwanted change events
        double sliderValue = Math.Round(currentPage, 2);
        double sliderMaximum = totalPages + 0.5;
        if (PlaybackSlider.Maximum > sliderValue)
        {
            PlaybackSlider.Value = sliderValue;
            PlaybackSlider.Maximum = sliderMaximum;
        }
        else
        {
            PlaybackSlider.Maximum = sliderMaximum;
            PlaybackSlider.Value = sliderValue;
        }
    }

    //
    // Other UI
    //

    private void UpdatePinRelatedUI()
    {
        if (ViewModel.IsPinned)
        {
            MainReaderView.Margin = new Thickness(0, _topOverlayHeight, _rightOverlayWidth, _bottomGridHeight);
            ShowOverlay();
        }
        else
        {
            MainReaderView.Margin = new Thickness(0);
        }
    }

    private void OnGridViewItemClicked(object sender, ItemClickEventArgs e)
    {
        var ctx = (ReaderPreviewImageViewModel)e.ClickedItem;
        GridViewModeEnabled = false;

        ReaderView reader = MainReaderView;
        int frameIndex = reader.GetFrameIndexByPage(ctx.Page);
        if (frameIndex >= 0)
        {
            MainReaderView.SetFrameIndex(frameIndex);
        }
        else
        {
            MainReaderView.SetPage(ctx.Page);
        }
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

    private void SyncCurrentComic()
    {
        ComicModel? comic = ViewModel.Comic;

        if (comic is not null)
        {
            _readerNavigationBar.SetReaderSettings(comic);
        }

        HashSet<int> pageIndices = GetCurrentPageIndices();
        CoroutineUtils.Run(async () =>
        {
            ComicChangedEventArgs args = new()
            {
                Comic = comic,
                Playlist = ViewModel.Playlist,
                ImageDescriptions = await ViewModel.GetImageDescriptions(pageIndices),
            };
            GetWindowEventBus().With<ComicChangedEventArgs>(EventId.ComicInfoChanged).Emit(args);
        });

        if (comic is not null)
        {
            foreach (PluginContext plugin in PluginManager.Instance.GetActivePlugins())
            {
                plugin.SetReadingComic(GetMainWindowAbility().PluginWindowContext, comic);
            }
        }
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

    private HashSet<int> GetCurrentPageIndices()
    {
        double page = MainReaderView.CurrentPage;
        int pageCount = MainReaderView.PageCount;

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

    public class ReaderStatusInfo
    {
        public required ReaderStatusEnum Status { get; init; }
        public string Description { get; init; } = string.Empty;
        public int Progress { get; init; } = -1;
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
