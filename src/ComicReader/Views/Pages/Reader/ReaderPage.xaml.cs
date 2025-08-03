// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.BaseUI;
using ComicReader.Common.Constants;
using ComicReader.Common.Imaging;
using ComicReader.Common.Legacy;
using ComicReader.Common.Lifecycle;
using ComicReader.Common.Threading;
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
using ComicReader.Views.Pages.Main;
using ComicReader.Views.Pages.Navigation;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;

namespace ComicReader.Views.Pages.Reader;

internal sealed partial class ReaderPage : BasePage
{
    //
    // Constants
    //

    private const string KEY_TIP_SHOWN = "ReaderTipShown";
    private const string REGEX_URL = "(https?:\\/\\/)?(www\\.)?[-a-zA-Z0-9@:%._\\+~#=]{2,256}\\.[a-z]{2,6}\\b([-a-zA-Z0-9@:%_\\+.~#?&//=]*)";

    //
    // Variables
    //

    private ComicModel? _comic;
    private ComicModel? _pendingComic;
    private bool _isLoading = false;

    private IComicConnection? _comicConnection;
    private volatile bool _updatingProgress = false;
    private bool? _isFavorite = null;
    private ComicCompletionStatusEnum? _completionState = null;

    private bool _buttomTileShowed = false;
    private bool _buttomTileHold = false;
    private bool _buttomTilePointerIn = false;
    private DateTimeOffset _buttomTileHideRequestTime = DateTimeOffset.Now;

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
        ViewModel.ComicTags = new ObservableCollection<TagCollectionViewModel>();
        ViewModel.IsEditable = false;
        ViewModel.PreviewDataSource = new ObservableCollection<ReaderImagePreviewViewModel>();

        ReaderView reader = MainReaderView;

        reader.ReaderEventTapped += delegate (ReaderView sender)
        {
            BottomTileSetHold(!_buttomTileShowed);
        };

        reader.ReaderEventPageChanged += delegate (ReaderView sender, bool isIntermediate)
        {
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
                    BottomTileShow();
                    BottomTileHide(5000);
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

        C0.Run(async delegate
        {
            long comicId = bundle.GetLong(RouterConstants.ARG_COMIC_ID, -1);
            ComicModel? comic = await ComicModel.FromId(comicId, "ReaderGetComic");
            if (comic == null)
            {
                string token = bundle.GetString(RouterConstants.ARG_COMIC_TOKEN, "");
                comic = AppModel.GetComicData(token);
            }

            if (comic != null)
            {
                GetMainPageAbility().SetTitle(comic.Title);
            }
            GetMainPageAbility().SetIcon(new SymbolIconSource { Symbol = Symbol.Pictures });

            if (comic != null)
            {
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
        SetAsReadingComic();
        LoadComicInfo();
    }

    protected override void OnStop()
    {
        base.OnStop();
        ReleaseComicConnection();
    }

    private void ObserveData()
    {
        GetMainPageAbility().RegisterTabUnselectedHandler(this, AppModel.UnsetReadingComic);
        GetNavigationPageAbility().RegisterLeavingHandler(this, AppModel.UnsetReadingComic);

        ViewModel.TagClickLiveData.Observe(this, (string tag) =>
        {
            Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SEARCH)
                .WithParam(RouterConstants.ARG_KEYWORD, "<tag: " + tag + ">");
            GetMainPageAbility().OpenInNewTab(route);
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

        GetMainPageAbility().RegisterFullscreenChangedHandler(this, delegate (bool isFullscreen)
        {
            ViewModel.IsFullscreen = isFullscreen;
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

        GetNavigationPageAbility().RegisterReaderSettingsChangedEventHandler(this, delegate (ReaderSettingDataModel setting)
        {
            ComicModel? comic = _comic;
            if (comic != null && !comic.IsExternal)
            {
                comic.SetExt(ComicExt.USE_DEFAULT_READER_SETTINGS, setting.UseDefault ? "1" : "0");
                comic.SetExt(ComicExt.VERTICAL_READING, setting.IsVertical ? "1" : "0");
                comic.SetExt(ComicExt.LEFT_TO_RIGHT, setting.IsLeftToRight ? "1" : "0");
                comic.SetExt(ComicExt.VERTICAL_CONTINUOUS, setting.IsVerticalContinuous ? "1" : "0");
                comic.SetExt(ComicExt.HORIZONTAL_CONTINUOUS, setting.IsHorizontalContinuous ? "1" : "0");
                comic.SetExt(ComicExt.VERTICAL_PAGE_ARRANGEMENT, setting.VerticalPageArrangement.ToString());
                comic.SetExt(ComicExt.HORIZONTAL_PAGE_ARRANGEMENT, setting.HorizontalPageArrangement.ToString());
                comic.SetExt(ComicExt.PAGE_GAP, setting.PageGap.ToString());
                comic.FlushExt();
            }

            ApplyReaderSettings(setting);
            UpdateReaderUI();
        });

        IsExternalComicLiveData.ObserveSticky(this, delegate (bool isExternal)
        {
            RcRating.Visibility = isExternal ? Visibility.Collapsed : Visibility.Visible;
            FavoriteBt.IsEnabled = !isExternal;
            SetCompletionStateButton.Visibility = isExternal ? Visibility.Collapsed : Visibility.Visible;
            GetNavigationPageAbility().SetExternalComic(isExternal);
        });

        GetNavigationPageAbility().RegisterFavoriteChangedEventHandler(this, delegate (bool isFavorite)
        {
            SetIsFavorite(isFavorite, true);
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

    //
    // Loader
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

        ReleaseComicConnection();
        _comic = null;

        if (comic == null)
        {
            ReaderStatusLiveData.Emit(ReaderStatusEnum.Error);
            return;
        }

        if (!comic.IsExternal)
        {
            await comic.SetCompletionStateToAtLeastStarted();
            HistoryModel.Instance.Add(comic.Id, comic.Title1, true);
        }

        _comic = comic;
        SetAsReadingComic();
        LoadReaderSettings();
        LoadComicInfo();
        IComicConnection? connection = await comic.OpenComicAsync();
        _comicConnection = connection;

        if (connection == null)
        {
            ReaderStatusLiveData.Emit(ReaderStatusEnum.Error);
            return;
        }

        ReaderStatusLiveData.Emit(ReaderStatusEnum.Loading);

        if (!comic.IsExternal)
        {
            TaskException result = await comic.ReloadImageFiles();
            if (!result.Successful())
            {
                Log("Failed to load images of '" + comic.Location + "'. " + result.ToString());
                ReaderStatusLiveData.Emit(ReaderStatusEnum.Error);
                return;
            }

            MainReaderView.SetInitialPage(comic.LastPosition);
        }

        var images = new List<IImageSource>();
        for (int i = 0; i < connection.GetImageCount(); ++i)
        {
            images.Add(new ComicImageSource(comic, connection, i));
        }
        MainReaderView.StartLoadingImages(images);

        // Update previews
        double preview_width = (double)Application.Current.Resources["ReaderPreviewImageWidth"];
        double preview_height = (double)Application.Current.Resources["ReaderPreviewImageHeight"];
        ViewModel.PreviewDataSource.Clear();
        for (int i = 0; i < connection.GetImageCount(); ++i)
        {
            ViewModel.PreviewDataSource.Add(new ReaderImagePreviewViewModel
            {
                Image = new SimpleImageView.Model
                {
                    Source = new ComicImageSource(comic, connection, i),
                    Width = preview_width,
                    Height = preview_height,
                    Dispatcher = _loadPreviewDispatcher,
                    DebugDescription = i.ToString()
                },
                Page = i + 1,
            });
        }
    }

    private void ReleaseComicConnection()
    {
        _comicConnection?.Dispose();
        _comicConnection = null;
    }

    private void SetAsReadingComic()
    {
        ComicModel? comic = _comic;
        if (comic != null && !comic.IsExternal)
        {
            AppModel.SetReadingComic(comic.Id);
        }
    }

    private void LoadReaderSettings()
    {
        ComicModel? comic = _comic;
        if (comic == null)
        {
            return;
        }
        ReaderSettingDataModel readerSettingModel = GetReaderSettingModel(comic);
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
        reader.SetPageGap(readerSettingModel.PageGap);
    }

    private void LoadComicInfo()
    {
        if (_comic == null)
        {
            return;
        }

        _isExternalComicLiveData.Emit(_comic.IsExternal);

        if (_comic.Title1.Length == 0)
        {
            ViewModel.ComicTitle1 = _comic.Title;
        }
        else
        {
            ViewModel.ComicTitle1 = _comic.Title1;
            ViewModel.ComicTitle2 = _comic.Title2;
        }

        LoadDescription(_comic.Description);
        ViewModel.ComicDir = _comic.Location;
        ViewModel.IsEditable = _comic.IsEditable;

        LoadComicTag();

        bool isFavorite = !_comic.IsExternal && FavoriteModel.Instance.FromId(_comic.Id) != null;
        SetIsFavorite(isFavorite, false);

        SetCompletionState(_comic.CompletionState, false);

        if (!_comic.IsExternal)
        {
            ViewModel.Rating = _comic.Rating;
        }
    }

    private void LoadDescription(string description)
    {
        InlineCollection inlines = TbComicDescription.Inlines;
        inlines.Clear();

        Regex urlRegex = new(REGEX_URL, RegexOptions.None);
        MatchCollection matches = urlRegex.Matches(description);
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
                    Text = description[currentIndex..startIndex]
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

        if (currentIndex < description.Length)
        {
            var run = new Run
            {
                Text = description[currentIndex..]
            };
            inlines.Add(run);
        }

        if (inlines.Count == 0)
        {
            TbComicDescription.Visibility = Visibility.Collapsed;
        }
        else
        {
            TbComicDescription.Visibility = Visibility.Visible;
        }
    }

    private void LoadComicTag()
    {
        if (_comic == null)
        {
            return;
        }

        var new_collection = new ObservableCollection<TagCollectionViewModel>();

        for (int i = 0; i < _comic.Tags.Count; ++i)
        {
            ComicData.TagData tags = _comic.Tags[i];
            var tags_model = new TagCollectionViewModel(tags.Name);

            foreach (string tag in tags.Tags)
            {
                var tag_model = new TagViewModel
                {
                    Tag = tag,
                    OnClicked = () =>
                    {
                        ViewModel.TagClickLiveData.Emit(tag);
                    },
                };
                tags_model.Tags.Add(tag_model);
            }

            new_collection.Add(tags_model);
        }

        ViewModel.ComicTags = new_collection;
    }

    private ReaderSettingDataModel GetReaderSettingModel(ComicModel comic)
    {
        PageArrangementEnum? ParsePageArrangement(string? value)
        {
            if (value == null)
            {
                return null;
            }
            if (Enum.TryParse(value, out PageArrangementEnum arrangement))
            {
                return arrangement;
            }
            return null;
        }

        AppSettingsModel.ReaderSettingModel readerSettings = AppSettingsModel.Instance.GetModel().DefaultReaderSetting;
        bool useDefault = comic.GetExt(ComicExt.USE_DEFAULT_READER_SETTINGS)?.Equals("1") ?? true;
        bool verticalReading;
        bool leftToRight;
        bool verticalContinuous;
        bool horizontalContinuous;
        PageArrangementEnum verticalPageArrangement;
        PageArrangementEnum horizontalPageArrangement;
        int pageGap;

        if (useDefault)
        {
            verticalReading = readerSettings.VerticalReading;
            leftToRight = readerSettings.LeftToRight;
            verticalContinuous = readerSettings.VerticalContinuous;
            horizontalContinuous = readerSettings.HorizontalContinuous;
            verticalPageArrangement = readerSettings.VerticalPageArrangement;
            horizontalPageArrangement = readerSettings.HorizontalPageArrangement;
            pageGap = readerSettings.PageGap;
        }
        else
        {
            verticalReading = comic.GetExt(ComicExt.VERTICAL_READING)?.Equals("1") ?? readerSettings.VerticalReading;
            leftToRight = comic.GetExt(ComicExt.LEFT_TO_RIGHT)?.Equals("1") ?? readerSettings.LeftToRight;
            verticalContinuous = comic.GetExt(ComicExt.VERTICAL_CONTINUOUS)?.Equals("1") ?? readerSettings.VerticalContinuous;
            horizontalContinuous = comic.GetExt(ComicExt.HORIZONTAL_CONTINUOUS)?.Equals("1") ?? readerSettings.HorizontalContinuous;
            verticalPageArrangement = ParsePageArrangement(comic.GetExt(ComicExt.VERTICAL_PAGE_ARRANGEMENT)) ?? readerSettings.VerticalPageArrangement;
            horizontalPageArrangement = ParsePageArrangement(comic.GetExt(ComicExt.HORIZONTAL_PAGE_ARRANGEMENT)) ?? readerSettings.HorizontalPageArrangement;

            pageGap = readerSettings.PageGap;
            {
                string? pageGapString = comic.GetExt(ComicExt.PAGE_GAP);
                if (!string.IsNullOrEmpty(pageGapString) && int.TryParse(pageGapString, out int parsedPageGap))
                {
                    pageGap = parsedPageGap;
                }
            }
        }

        return new ReaderSettingDataModel
        {
            UseDefault = useDefault,
            IsVertical = verticalReading,
            IsLeftToRight = leftToRight,
            IsVerticalContinuous = verticalContinuous,
            IsHorizontalContinuous = horizontalContinuous,
            VerticalPageArrangement = verticalPageArrangement,
            HorizontalPageArrangement = horizontalPageArrangement,
            PageGap = pageGap,
        };
    }

    //
    // UI
    //

    public void UpdateReaderUI()
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
        C0.Run(async delegate
        {
            ComicModel? comic = _comic;
            if (comic == null)
            {
                return;
            }

            var dialog = new EditComicInfoDialog([comic]);
            ContentDialogResult result = await dialog.ShowAsync(XamlRoot);
            if (result == ContentDialogResult.Primary)
            {
                LoadComicInfo();
            }
        });
    }

    private void OnBottomGridPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        OnReaderPointerExited();
    }

    private void OnInfoPanePointerEntered(object sender, PointerRoutedEventArgs e)
    {
        OnReaderPointerExited();
    }

    private void OnTitleBarAreaPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        OnReaderPointerExited();
    }

    private void OnReaderPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _buttomTilePointerIn = false;

        if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
        {
            return;
        }

        if (!_buttomTileShowed || _buttomTileHold)
        {
            return;
        }

        BottomTileHide(3000);
    }

    private void OnReaderTipCloseButtonClick(InfoBar sender, object args)
    {
        KVDatabase.Default.SetBoolean(DatabaseEntry.KV_LIB_TIPS, KEY_TIP_SHOWN, true);
    }

    private void OnFullscreenBtClicked(object sender, RoutedEventArgs e)
    {
        GetMainPageAbility().EnterFullscreen();
        ViewModel.IsFullscreen = true;
    }

    private void OnBackToWindowBtClicked(object sender, RoutedEventArgs e)
    {
        GetMainPageAbility().ExitFullscreen();
        ViewModel.IsFullscreen = false;
    }

    private void OnGridViewContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        var item = args.Item as ReaderImagePreviewViewModel;
        var viewHolder = args.ItemContainer.ContentTemplateRoot as ReaderPreviewImage;
        viewHolder?.SetModel(item, args.InRecycleQueue);
    }

    //
    // Bottom Tile
    //

    private void OnReaderPointerExited()
    {
        _buttomTilePointerIn = true;
        BottomTileShow();
    }

    public void BottomTileShow()
    {
        if (_buttomTileShowed)
        {
            return;
        }

        GetMainPageAbility().ShowOrHideTitleBar(true);
        _buttomTileShowed = true;
    }

    public void BottomTileHide(int timeout)
    {
        _buttomTileHideRequestTime = DateTimeOffset.Now;

        if (timeout > 0)
        {
            _ = Task.Run(() =>
            {
                Task.Delay(timeout + 1).Wait();

                if ((DateTimeOffset.Now - _buttomTileHideRequestTime).TotalMilliseconds < timeout)
                {
                    return;
                }

                _ = MainThreadUtils.RunInMainThread(delegate
                {
                    BottomTileHide(0);
                });
            });
            return;
        }

        if (!_buttomTileShowed || _buttomTileHold || _buttomTilePointerIn || InfoPane.IsPaneOpen)
        {
            return;
        }

        if (GridViewModeEnabled)
        {
            return;
        }

        if (GetNavigationPageAbility().GetIsSidePaneOpen())
        {
            return;
        }

        BottomGridForceHide();
    }

    private void BottomGridForceHide()
    {
        GetMainPageAbility().ShowOrHideTitleBar(false);
        _buttomTileShowed = false;
        _buttomTileHold = false;
    }

    private void BottomTileSetHold(bool val)
    {
        _buttomTileHold = val;

        if (_buttomTileHold)
        {
            BottomTileShow();
        }
        else
        {
            BottomTileHide(0);
        }
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

    public void Log(string message)
    {
        Logger.I("ReaderPage", message);
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
