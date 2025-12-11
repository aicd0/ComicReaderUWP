// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Actions;
using ComicReader.Common.Actions.Providers;
using ComicReader.Common.BaseUI;
using ComicReader.Common.Constants;
using ComicReader.Common.Legacy;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.KVStorage;
using ComicReader.SDK.Common.Utils;
using ComicReader.ViewModels;
using ComicReader.Views.AppWindows.Main;
using ComicReader.Views.Dialogs.EditComicInfo;
using ComicReader.Views.Dialogs.EditTag;
using ComicReader.Views.Pages.Main;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;

using Windows.Storage;
using Windows.Win32;

namespace ComicReader.Views.Pages.Reader;

internal sealed partial class ReaderPage : BasePage
{
    private const string TAG = nameof(ReaderPage);
    private const string KEY_TIP_SHOWN = "ReaderTipShown";
    private const string REGEX_URL = @"https?:\/\/[a-zA-Z0-9\-._~%]+(?:\.[a-zA-Z0-9\-._~%]+)+(?:\/[^\s]*)?";
    private const int SAVE_PREOGRESS_INTERVAL = 500;

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
            UpdateReaderUI();
            GetNavigationPageAbility().SetGridViewMode(value);
        }
    }

    private bool _restoreSidebar = false;
    private bool _readerPointerEntered = true;
    private bool _bottomTileShowed = false;
    private bool _bottomTileHold = false;
    private long _bottomTileTargetHideTime = -1;

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
        ViewModel.PreviewDataSource = [];

        ReaderView reader = MainReaderView;

        reader.ReaderEventTapped += delegate (ReaderView sender)
        {
            BottomTileSetHold(!_bottomTileShowed);
        };

        reader.ReaderEventPageChanged += delegate (ReaderView sender, bool isIntermediate)
        {
            ViewModel.SetPageIndex(MainReaderView.CurrentPageDisplay - 1);
            UpdatePage();

            if (!reader.IsAutoScrolling)
            {
                BottomTileSetHold(false);
            }

            if (!isIntermediate)
            {
                SaveProgress();
            }
        };

        reader.ReaderEventReaderStateChanged += delegate (ReaderView sender, ReaderView.ReaderState state, string description)
        {
            switch (state)
            {
                case ReaderView.ReaderState.Ready:
                    ViewModel.ReaderStatusLiveData.Emit(new(ReaderStatusEnum.Working, description));
                    UpdatePage();
                    ShowBottomTile();
                    HideBottomTileDelayed(5000);
                    break;
                case ReaderView.ReaderState.Loading:
                    ViewModel.ReaderStatusLiveData.Emit(new(ReaderStatusEnum.Loading, description));
                    break;
                case ReaderView.ReaderState.Error:
                    ViewModel.ReaderStatusLiveData.Emit(new(ReaderStatusEnum.Error, description));
                    break;
            }
        };

        reader.ReaderEventAutoScrollingChanged += delegate (ReaderView sender, bool isAutoScrolling)
        {
            if (isAutoScrolling)
            {
                ViewModel.ReaderCommonStatus = StringResource.Auto;
            }
            else
            {
                ViewModel.ReaderCommonStatus = "";
            }
        };
    }

    //
    // Lifecycle
    //

    protected override void OnStart(PageBundle bundle)
    {
        base.OnStart(bundle);

        ViewModel.Initialize(PageActionHandler);

        bool tipShown = KVDatabase.Default.GetBoolean(DatabaseEntry.KV_LIB_TIPS, KEY_TIP_SHOWN, false);
        if (!tipShown)
        {
            ReaderTip.IsOpen = !tipShown;
        }

        GetMainPageAbility().SetIcon(new SymbolIconSource { Symbol = Symbol.Pictures });
        CoroutineUtils.Start(async () =>
        {
            ComicModel? comic = await GetTargetComic(bundle);
            if (comic is null)
            {
                GetMainPageAbility().SetTitle(StringResource.Error);
                ViewModel.ReaderStatusLiveData.Emit(new(ReaderStatusEnum.Error));
            }
            else
            {
                GetMainPageAbility().SetTitle(comic.Title);
                await ViewModel.LoadComic(comic);
            }
        });

        ObserveData();
    }

    protected override void OnResume()
    {
        base.OnResume();

        ViewModel.ReloadReaderSettings();
        UpdateReaderUI();

        // Take focus from sidebar
        GetMainPageAbility().SetSidePaneOpenState(false, force: false);
        MainReaderView.Focus(FocusState.Programmatic);
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

        GetEventBus().With<double>(EventId.TopOverlayHeight).ObserveSticky(this, h =>
        {
            Thickness margin = PreviewGridView.Margin;
            margin.Top = h;
            PreviewGridView.Margin = margin;
            InfoPane.Margin = new Thickness(0, h, 0, 0);
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
            FullscreenButtonGrid.Opacity = opacity;
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

        GetNavigationPageAbility().RegisterGridViewModeChangedHandler(this, delegate (bool enabled)
        {
            GridViewModeEnabled = enabled;
        });

        GetNavigationPageAbility().RegisterExpandInfoPaneHandler(this, delegate
        {
            InfoPane.IsPaneOpen = true;
            _restoreSidebar = GetMainPageAbility().GetSidePaneOpenState();
            GetMainPageAbility().SetSidePaneOpenState(false, force: true);
        });

        GetNavigationPageAbility().RegisterReaderSettingsChangedEventHandler(this, delegate (ReaderSettingDataModel setting)
        {
            ApplyReaderSettings(setting);
            UpdateReaderUI();
        });

        GetNavigationPageAbility().RegisterFavoriteChangedEventHandler(this, delegate (bool isFavorite)
        {
            ViewModel.SetIsFavorite(isFavorite, true);
        });

        ViewModel.EditTagLiveData.Observe(this, pair =>
        {
            var dialog = new EditTagDialog(pair.Key, pair.Value);
            _ = dialog.ShowAsync(WindowId);
        });

        ViewModel.IsExternalComicLiveData.ObserveSticky(this, delegate (bool isExternal)
        {
            RcRating.Visibility = isExternal ? Visibility.Collapsed : Visibility.Visible;
            SetCompletionStateButton.Visibility = isExternal ? Visibility.Collapsed : Visibility.Visible;
            GetNavigationPageAbility().SetExternalComic(isExternal);
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

        ViewModel.ReaderSettingLiveData.ObserveSticky(this, comic =>
        {
            GetNavigationPageAbility().SetReaderSettings(comic);
        });

        ViewModel.ComicDescriptionLiveData.ObserveSticky(this, description =>
        {
            FillRichTextInlines(TbComicDescription.Inlines, description);
            TbComicDescription.Visibility = TbComicDescription.Inlines.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        });

        ViewModel.IsFavoriteLiveData.ObserveSticky(this, isFavorite =>
        {
            GetNavigationPageAbility().SetFavorite(isFavorite);
        });

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
            MainReaderView.SetCurrentPage(info.InitialPage);
            MainReaderView.StartLoadingImages(info.Images);
        });
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
                    double page = reader.CurrentPage;
                    if (page <= 0.0)
                    {
                        continue;
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
                    await comic.SaveProgressAsync(progress, page);
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
    }

    private void UpdateReaderUI()
    {
        bool isWorking = ViewModel.ReaderStatusLiveData.GetValue()?.Status == ReaderStatusEnum.Working;
        bool previewVisible = isWorking && _gridViewModeEnabled;
        bool readerVisible = isWorking && !previewVisible;

        // Using Visibility.Collapsed here will prevent GridView from loading early
        PreviewGridView.Opacity = previewVisible ? 1.0 : 0.0;
        PreviewGridView.IsHitTestVisible = previewVisible;

        GMainSection.Visibility = previewVisible ? Visibility.Collapsed : Visibility.Visible;
        MainReaderView.SetVisibility(readerVisible);
    }

    private void UpdatePage()
    {
        ReaderView reader = MainReaderView;
        int currentPage = reader.CurrentPageDisplay;
        PageIndicator.Text = currentPage.ToString() + " / " + reader.PageCount.ToString();
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

        if (_bottomTileHold || InfoPane.IsPaneOpen || GridViewModeEnabled ||
            ViewModel.ReaderStatusLiveData.GetValue()?.Status != ReaderStatusEnum.Working)
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
        MainReaderView.SetCurrentPage(ctx.Page);
    }

    private void NewTagsTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            var textBox = (TextBox)sender;
            string text = textBox.Text;
            textBox.Text = string.Empty;
            ViewModel.AddNewTags(text);
            e.Handled = true;
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

        List<BaseMenuFlyoutItemViewModel> menuItems = await MenuFlyoutItemsCreator.CreateMenuItems(comic, PageActionHandler);
        if (menuItems.Count == 0)
        {
            return;
        }

        var flyout = new MenuFlyout();
        foreach (BaseMenuFlyoutItemViewModel item in menuItems)
        {
            flyout.Items.Add(item.CreateMenuFlyoutItem());
        }

        flyout.ShowAt(fe, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight });
    }

    private void OnRatingControlValueChanged(RatingControl sender, object args)
    {
        ViewModel.Comic?.SaveRating((int)sender.Value);
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
        _ = dialog.ShowAsync(WindowId);
    }

    private void OnReaderPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_readerPointerEntered)
        {
            return;
        }

        _readerPointerEntered = false;
        if (IsPointerInsideWindow())
        {
            ShowBottomTile();
        }
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
        KVDatabase.Default.SetBoolean(DatabaseEntry.KV_LIB_TIPS, KEY_TIP_SHOWN, true);
    }

    private void OnFullscreenBtClicked(object sender, RoutedEventArgs e)
    {
        GetMainWindowAbility().EnterFullscreen();
    }

    private void OnBackToWindowBtClicked(object sender, RoutedEventArgs e)
    {
        GetMainWindowAbility().ExitFullscreen();
    }

    private void OnGridViewContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        var item = args.Item as ReaderImagePreviewViewModel;
        var viewHolder = args.ItemContainer.ContentTemplateRoot as ReaderPreviewImage;
        viewHolder?.SetModel(item, args.InRecycleQueue);
    }

    private void InfoPane_PaneClosed(SplitView sender, object args)
    {
        if (_restoreSidebar)
        {
            _restoreSidebar = false;
            GetMainPageAbility().SetSidePaneOpenState(true, force: false);
        }
    }

    //
    // Utilities
    //

    private IMainWindowAbility GetMainWindowAbility()
    {
        return GetAbility<IMainWindowAbility>()!;
    }

    private IMainPageAbility GetMainPageAbility()
    {
        return GetAbility<IMainPageAbility>()!;
    }

    private INavigationPageAbility GetNavigationPageAbility()
    {
        return GetAbility<INavigationPageAbility>()!;
    }

    private bool IsPointerInsideWindow()
    {
        MainWindow? window = App.Instance.WindowManager.GetWindow(WindowId);
        if (window is null)
        {
            return true;
        }

        Windows.Win32.Foundation.HWND hWnd = new(window.WindowHandle);
        PInvoke.GetCursorPos(out System.Drawing.Point pos);
        PInvoke.ScreenToClient(hWnd, ref pos);
        PInvoke.GetClientRect(hWnd, out Windows.Win32.Foundation.RECT rect);
        bool inside =
            pos.X >= rect.left && pos.X < rect.right &&
            pos.Y >= rect.top && pos.Y < rect.bottom;
        return inside;
    }

    private static async Task<ComicModel?> GetTargetComic(PageBundle bundle)
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
            return KVDatabase.Default.GetString(DatabaseEntry.KV_LIB_READER_STATE, key);
        }

        public void WriteConfiguration(string key, string value)
        {
            KVDatabase.Default.SetString(DatabaseEntry.KV_LIB_READER_STATE, key, value);
        }
    }
}
