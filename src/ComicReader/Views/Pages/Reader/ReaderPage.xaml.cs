// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.BaseUI;
using ComicReader.Common.Constants;
using ComicReader.Common.Legacy;
using ComicReader.Common.Utils;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.KVStorage;
using ComicReader.ViewModels;
using ComicReader.Views.Dialogs.EditComicInfo;
using ComicReader.Views.Dialogs.EditTag;
using ComicReader.Views.Pages.Main;
using ComicReader.Views.Pages.Navigation;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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

    private volatile bool _updatingProgress = false;

    private bool _bottomTileShowed = false;
    private bool _bottomTileHold = false;
    private long _bottomTileTargetHideTime = -1;

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
            UpdateProgress(sender, save: !isIntermediate);

            if (!reader.IsAutoScrolling)
            {
                BottomTileSetHold(false);
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
                ViewModel.ReaderCommonStatus = "Auto";
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
            if (comic != null)
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

        GetNavigationPageAbility().SetGridViewMode(false);
        ViewModel.ReloadReaderSettings();
        UpdateReaderUI();
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

        GetEventBus().With<double>(EventId.TitleBarHeightChange).ObserveSticky(this, delegate (double h)
        {
            TitleBarArea.Height = h;
            PreviewTitleBarPlaceHolder.Height = h;
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
            ComicModel? comic = ViewModel.Comic;
            if (comic != null && !comic.IsExternal)
            {
                setting.To(comic);
            }

            ApplyReaderSettings(setting);
            UpdateReaderUI();
        });

        GetNavigationPageAbility().RegisterFavoriteChangedEventHandler(this, delegate (bool isFavorite)
        {
            ViewModel.SetIsFavorite(isFavorite, true);
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

        ViewModel.IsExternalComicLiveData.ObserveSticky(this, delegate (bool isExternal)
        {
            RcRating.Visibility = isExternal ? Visibility.Collapsed : Visibility.Visible;
            SetCompletionStateButton.Visibility = isExternal ? Visibility.Collapsed : Visibility.Visible;
            GetNavigationPageAbility().SetExternalComic(isExternal);
        });

        ViewModel.ReaderStatusLiveData.Observe(this, delegate (ReaderStatusInfo info)
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

        ViewModel.ReaderSettingLiveData.Observe(this, setting =>
        {
            GetNavigationPageAbility().SetReaderSettings(setting);
            ApplyReaderSettings(setting);
        });

        ViewModel.ComicDescriptionLiveData.Observe(this, description =>
        {
            FillRichTextInlines(TbComicDescription.Inlines, description);
            TbComicDescription.Visibility = TbComicDescription.Inlines.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        });

        ViewModel.IsFavoriteLiveData.Observe(this, isFavorite =>
        {
            GetNavigationPageAbility().SetFavorite(isFavorite);
        });

        ViewModel.CompletionStateLiveData.Observe(this, completionStatus =>
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

        ViewModel.ReaderLoadingInfoLiveData.Observe(this, info =>
        {
            MainReaderView.SetConfigurationDatabase(new ReaderConfigDatabase());
            MainReaderView.SetCurrentPage(info.InitialPage);
            MainReaderView.StartLoadingImages(info.Images);
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
    // Reader Settings
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

    //
    // UI
    //

    private void UpdateReaderUI()
    {
        bool isWorking = ViewModel.ReaderStatusLiveData.GetValue()?.Status == ReaderStatusEnum.Working;
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
                ViewModel.Comic?.SaveProgressAsync(progress, page).Wait();
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

        if (_bottomTileHold || InfoPane.IsPaneOpen || GridViewModeEnabled ||
            GetNavigationPageAbility().GetIsSidePaneOpen() ||
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

    private void MarkAsUnreadButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SetCompletionState(ComicCompletionStatusEnum.NotStarted, true);
    }

    private void MarkAsReadingButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SetCompletionState(ComicCompletionStatusEnum.Started, true);
    }

    private void MarkAsFinishedButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SetCompletionState(ComicCompletionStatusEnum.Completed, true);
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

    private void OnFullscreenBtClicked(object sender, RoutedEventArgs e)
    {
        GetMainPageAbility().EnterFullscreen();
    }

    private void OnBackToWindowBtClicked(object sender, RoutedEventArgs e)
    {
        GetMainPageAbility().ExitFullscreen();
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
