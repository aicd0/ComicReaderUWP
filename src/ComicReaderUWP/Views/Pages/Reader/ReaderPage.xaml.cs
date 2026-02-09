// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Actions.Providers;
using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.BaseUI.PageAbilities;
using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Utils;
using ComicReaderUWP.SDK.Database.Registry;
using ComicReaderUWP.UserControls.Reader;
using ComicReaderUWP.ViewModels;
using ComicReaderUWP.Views.Dialogs.EditComicInfo;
using ComicReaderUWP.Views.Dialogs.EditTag;
using ComicReaderUWP.Views.Pages.Main;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;

namespace ComicReaderUWP.Views.Pages.Reader;

internal sealed partial class ReaderPage : BasePage
{
    private const string TAG = nameof(ReaderPage);
    private const string REGEX_URL = @"https?:\/\/[a-zA-Z0-9\-._~%]+(?:\.[a-zA-Z0-9\-._~%]+)+(?:\/[^\s]*)?";
    private const int SAVE_PREOGRESS_INTERVAL = 500;

    // Must be accessed on UI thread
    public static IReadOnlyList<Tuple<int, string>> ActiveTabs { get; private set; } = [];

    //
    // Variables
    //

    private ReaderPageViewModel ViewModel { get; set; } = new();

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

    private readonly ReaderNavigationBar _readerNavigationBar;
    private bool _displayActive = false;
    private bool _restoreSidebar = false;
    private bool _readerPointerEntered = false;
    private bool _bottomTileShowed = false;
    private bool _bottomTileHold = false;
    private long _bottomTileTargetHideTime = -1;

    //
    // Constructor
    //

    public ReaderPage()
    {
        InitializeComponent();
        _readerNavigationBar = new();

        ViewModel.ComicTitle1 = "";
        ViewModel.ComicTitle2 = "";
        ViewModel.ComicDir = "";
        ViewModel.IsEditable = false;
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

        bool tipShown = AppDB.AppKV.GetCollection(KVNames.KV_LIB_TIPS).GetValueOrDefault(KVNames.KV_KEY_TIPS_READER_TIP_SHOWN, false);
        if (!tipShown)
        {
            ReaderTip.IsOpen = !tipShown;
        }

        MainReaderView.OverScrollEnabled = AppSettingsModel.Instance.AutoSwitch;
        _readerNavigationBar.SetWindowId(WindowId);
        _readerNavigationBar.SetZooming((int)Math.Round(MainReaderView.Zooming * 100F));

        ViewModel.Initialize(PageActionHandler);
        CoroutineUtils.Start(async () =>
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
        ViewModel.ReloadReaderSettings();
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
        GlobalEvent.Instance.ComicUpdated.Observe(this, delegate
        {
            ViewModel.ReloadComicInfo();
        });

        GlobalEvent.Instance.FavoriteUpdated.Observe(this, delegate
        {
            ViewModel.ReloadComicInfo();
        });

        GlobalEvent.Instance.TagInfoUpdated.Observe(this, delegate
        {
            ViewModel.ReloadComicInfo();
        });

        AppSettingsModel.Instance.KeepScreenOnBehaviorChangedLiveData.Observe(this, _ =>
        {
            UpdateDisplayStatus();
        });

        GetEventBus().With<double>(EventId.TopOverlayHeight).ObserveSticky(this, h =>
        {
            Thickness margin = PreviewGridView.Margin;
            margin.Top = h;
            PreviewGridView.Margin = margin;
            InfoPane.Margin = new Thickness(0, h, -2, 0);
        });

        GetEventBus().With<double>(EventId.RightOverlayWidth).ObserveSticky(this, w =>
        {
            Thickness margin = PreviewGridView.Margin;
            margin.Right = w;
            PreviewGridView.Margin = margin;
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

        GetMainWindowAbility().RegisterFullscreenChangedHandler(this, delegate (bool isFullscreen)
        {
            ViewModel.IsFullscreen = isFullscreen;
        });

        ViewModel.TitleLiveData.ObserveStartSticky(this, title =>
        {
            GetMainPageAbility().SetTitle(title);
        });

        ViewModel.PlaybackChangeLiveData.ObserveSticky(this, _ =>
        {
            Route route = Route.Create(GetMainPageAbility().Url)
                .WithParam(RouterConstants.ARG_PLAYBACK, ViewModel.Playback.ToSerializedString());
            GetMainPageAbility().SetUrl(route.Url);
        });

        ViewModel.EditTagLiveData.Observe(this, pair =>
        {
            var dialog = new EditTagDialog(pair.Key, pair.Value);
            CoroutineUtils.Start(() => dialog.ShowAsync(WindowId));
        });

        ViewModel.IsExternalComicLiveData.ObserveSticky(this, delegate (bool isExternal)
        {
            RcRating.Visibility = isExternal ? Visibility.Collapsed : Visibility.Visible;
            SetCompletionStateButton.Visibility = isExternal ? Visibility.Collapsed : Visibility.Visible;
            _readerNavigationBar.SetExternalComic(isExternal);
        });

        ViewModel.ReaderStatusLiveData.ObserveSticky(this, delegate (ReaderStatusInfo info)
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

        ViewModel.ReaderSettingLiveData.ObserveSticky(this, _readerNavigationBar.SetReaderSettings);

        ViewModel.ComicDescriptionLiveData.ObserveSticky(this, description =>
        {
            FillRichTextInlines(TbComicDescription.Inlines, description);
            TbComicDescription.Visibility = TbComicDescription.Inlines.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        });

        ViewModel.IsFavoriteLiveData.ObserveSticky(this, _readerNavigationBar.SetFavorite);

        ViewModel.CompletionStateLiveData.ObserveSticky(this, completionStatus =>
        {
            switch (completionStatus)
            {
                case ComicCompletionStatusEnum.NotStarted:
                    SetCompletionStateButton.Icon = new FontIcon
                    {
                        Glyph = "\uEA3A"
                    };
                    SetCompletionStateButton.Label = StringResource.CompletionStatusUnread;
                    break;
                case ComicCompletionStatusEnum.Started:
                    SetCompletionStateButton.Icon = new FontIcon
                    {
                        Glyph = "\uED5A"
                    };
                    SetCompletionStateButton.Label = StringResource.CompletionStatusReading;
                    break;
                case ComicCompletionStatusEnum.Completed:
                    SetCompletionStateButton.Icon = new FontIcon
                    {
                        Glyph = "\uE8FB"
                    };
                    SetCompletionStateButton.Label = StringResource.CompletionStatusFinished;
                    break;
                default:
                    break;
            }

            MarkAsUnreadButton.IsChecked = completionStatus == ComicCompletionStatusEnum.NotStarted;
            MarkAsReadingButton.IsChecked = completionStatus == ComicCompletionStatusEnum.Started;
            MarkAsFinishedButton.IsChecked = completionStatus == ComicCompletionStatusEnum.Completed;
        });

        ViewModel.ReaderLoadingInfoLiveData.ObserveSticky(this, info =>
        {
            MainReaderView.SetConfigurationDatabase(new ReaderConfigDatabase());
            MainReaderView.SetInitialPage(info.InitialPage);
            MainReaderView.StartLoadingImages(info.Images);
            FocusReader();
        });

        _readerNavigationBar.GridViewModeChanged += delegate (bool enabled)
        {
            GridViewModeEnabled = enabled;
        };

        _readerNavigationBar.InfoPaneExpanded += delegate
        {
            InfoPane.IsPaneOpen = true;
            _restoreSidebar = GetMainPageAbility().GetSidePaneOpenState();
            GetMainPageAbility().SetSidePaneOpenState(false, force: true);
        };

        _readerNavigationBar.ReaderSettingsChanged += ApplyReaderSettings;

        _readerNavigationBar.FavoriteChanged += delegate (bool isFavorite)
        {
            ViewModel.SetIsFavorite(isFavorite, true);
        };

        _readerNavigationBar.ZoomingChanged += delta =>
        {
            MainReaderView.Zooming += delta * 0.01F;
        };

        MainReaderView.ReaderEventTapped += delegate (ReaderView sender)
        {
            BottomTileSetHold(!_bottomTileShowed);
        };

        MainReaderView.ReaderEventPageChanged += delegate (ReaderView sender, bool isIntermediate)
        {
            ViewModel.SetPageIndex(MainReaderView.CurrentPageDisplay - 1);
            UpdatePage();

            if (!MainReaderView.IsAutoScrolling)
            {
                BottomTileSetHold(false);
            }

            if (!isIntermediate)
            {
                SaveProgress();
                AddToActiveTabs();
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
            _readerNavigationBar.SetZooming((int)Math.Round(zooming * 100F));
        };

        MainReaderView.ReaderEventAutoScrollingChanged += delegate (ReaderView sender, bool isAutoScrolling)
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

        CoroutineUtils.Start(async () =>
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

    private void ApplyReaderSettings(ReaderSettingDataModel readerSettingModel)
    {
        ReaderView reader = MainReaderView;
        reader.SetIsVertical(readerSettingModel.IsVertical);
        reader.SetIsContinuous(readerSettingModel.IsContinuous);
        reader.SetPageArrangement(readerSettingModel.PageArrangement);
        reader.SetFlowDirection(readerSettingModel.IsLeftToRight);
        reader.SetUseOriginalSize(readerSettingModel.OriginalSize);
        reader.SetAutoScrollSpeed(readerSettingModel.AutoScrollSpeed);
        reader.SetPageGap(readerSettingModel.PageGap);

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
        TryFocus(MainReaderView);
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

        if (_bottomTileHold || InfoPane.IsPaneOpen || GridViewModeEnabled || !_readerPointerEntered)
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
        double currentValue = reader.CurrentPageDisplay;
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
        int currentPage = reader.CurrentPageDisplay;
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
    // New tags
    //

    private readonly Lazy<SearchHistoryModel> _tagHistoryModel = new(() =>
    {
        return SearchHistoryModel.Get("NewTags");
    });

    private void NewTagsAutoSuggestBox_GotFocus(object sender, RoutedEventArgs e)
    {
        var autoSuggestBox = (AutoSuggestBox)sender;
        autoSuggestBox.ItemsSource = SearchTagHistory(autoSuggestBox.Text);
    }

    private void NewTagsAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        string text = sender.Text.Trim();
        sender.Text = string.Empty;
        if (ViewModel.AddNewTags(text))
        {
            _tagHistoryModel.Value.Save(text);
        }
    }

    private void NewTagsAutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        string text = args.SelectedItem.ToString() ?? string.Empty;
        sender.Text = string.Empty;
        if (ViewModel.AddNewTags(text))
        {
            _tagHistoryModel.Value.Save(text);
        }
    }

    private void NewTagsAutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            sender.ItemsSource = SearchTagHistory(sender.Text);
        }
    }

    private void NewTagTipButton_Click(object sender, RoutedEventArgs e)
    {
        ActionModel actionModel = ActionModel.Builder.Create(MessageDialogProvider.NAME)
            .AddParameter(MessageDialogProvider.PARAM_TITLE, StringResourceProvider.Instance.EnterNewTags)
            .AddParameter(MessageDialogProvider.PARAM_MESSAGE, StringResourceProvider.Instance.EnterNewTagsHint)
            .Build();
        PageActionHandler.Handle(actionModel);
    }

    private List<string> SearchTagHistory(string query)
    {
        char[] seperators = [.. LocalizationUtils.Colons, .. LocalizationUtils.Commas];
        string[] keywords = query.Split(seperators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return [.. _tagHistoryModel.Value.Search(keywords, 10)];
    }

    //
    // Events
    //

    private void OnGridViewItemClicked(object sender, ItemClickEventArgs e)
    {
        var ctx = (ReaderImagePreviewViewModel)e.ClickedItem;
        GridViewModeEnabled = false;
        MainReaderView.SetCurrentPage(ctx.Page);
    }

    private void MarkAsUnreadButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SetCompletionState(ComicCompletionStatusEnum.NotStarted);
    }

    private void MarkAsReadingButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SetCompletionState(ComicCompletionStatusEnum.Started);
    }

    private void MarkAsFinishedButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SetCompletionState(ComicCompletionStatusEnum.Completed);
    }

    private async void MoreAppBarButton_Click(object sender, RoutedEventArgs e)
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

        List<BaseMenuFlyoutItemModel> menuItems = await MenuFlyoutItemsCreator.CreateComicMenuItems(
            PageActionHandler, comic, ViewModel.Playlist.ToBuilder());
        if (menuItems.Count == 0)
        {
            return;
        }

        var flyout = new MenuFlyout();
        foreach (BaseMenuFlyoutItemModel item in menuItems)
        {
            flyout.Items.Add(item.CreateMenuFlyoutItem());
        }

        flyout.ShowAt(fe, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight });
    }

    private void OnRatingControlValueChanged(RatingControl sender, object args)
    {
        int value = (int)sender.Value;
        ViewModel.Comic?.SetRating(value < 1 ? -1 : value * 20);
    }

    private void OnDirectoryTapped(object sender, TappedRoutedEventArgs e)
    {
        var er = EventRecorder.Create("OnDirectoryTapped");
        ViewModel.Comic?.ShowInFileExplorer(er);
        er.DisplayErrorMessage(PageActionHandler);
    }

    private void OnEditInfoClick(object sender, RoutedEventArgs e)
    {
        ComicModel? comic = ViewModel.Comic;
        if (comic == null)
        {
            return;
        }

        var dialog = new EditComicInfoDialog([comic]);
        CoroutineUtils.Start(() => dialog.ShowAsync(WindowId));
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
            if (!_readerPointerEntered && GetMainWindowAbility().PointerInWindow())
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

        HideBottomTileDelayed(3000);
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

    private void InfoPane_PaneOpening(SplitView sender, object args)
    {
        GetNavigationPageAbility().SetFullscreenButtonVisible(false);
    }

    private void InfoPane_PaneClosed(SplitView sender, object args)
    {
        GetNavigationPageAbility().SetFullscreenButtonVisible(true);
        if (_restoreSidebar)
        {
            _restoreSidebar = false;
            GetMainPageAbility().SetSidePaneOpenState(true, force: false);
        }
    }

    private void PageIndicator_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        PointerPoint pt = e.GetCurrentPoint(null);
        int delta = -pt.Properties.MouseWheelDelta / (int)Windows.Win32.PInvoke.WHEEL_DELTA;
        int page = MainReaderView.CurrentPageDisplay + delta;
        if (page <= 0 || page > MainReaderView.PageCount)
        {
            return;
        }

        MainReaderView.SetCurrentPage(page);
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

        CoroutineUtils.Start(async () =>
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

    private static void TryFocus(UIElement element)
    {
        void helper(int attempts)
        {
            if (!element.IsHitTestVisible || element.Visibility != Visibility.Visible)
            {
                Logger.E(TAG, $"Failed to acquired focus for {element.GetType().Name} as it is not interactable");
                return;
            }

            attempts++;
            if (element.Focus(FocusState.Programmatic))
            {
                Logger.I(TAG, $"Acquired focus for {element.GetType().Name} after {attempts} attempts");
                return;
            }

            if (attempts >= 10)
            {
                Logger.E(TAG, $"Failed to acquired focus for {element.GetType().Name} after {attempts} attempts");
                return;
            }

            CoroutineUtils.PostInMainThreadAsync(async () =>
            {
                await Task.Delay(1);
                helper(attempts);
            }, Microsoft.UI.Dispatching.DispatcherQueuePriority.Low);
        }

        helper(0);
    }

    [GeneratedRegex(REGEX_URL, RegexOptions.None)]
    private static partial Regex URL_REGEX();

    private static void FillRichTextInlines(InlineCollection inlines, string richText)
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
