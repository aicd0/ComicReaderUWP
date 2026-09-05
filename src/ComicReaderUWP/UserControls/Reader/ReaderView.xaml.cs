// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Models.F8;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.UserControls.Reader.FrameLayout;
using ComicReaderUWP.UserControls.Reader.Imaging;
using ComicReaderUWP.UserControls.Reader.PageLayout;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace ComicReaderUWP.UserControls.Reader;

internal partial class ReaderView : UserControl
{
    #region Constants

    private const string TAG = nameof(ReaderView);
    private const float MAX_ZOOM = 2.5F;
    private const float MIN_ZOOM_CENTER_INSIDE = 0.5F;
    private const float MIN_ZOOM_CENTER_CROP = 0.2F;
    private const double DEFAULT_IMAGE_SIDE_LENGTH = 500.0;
    private const double DEFAULT_VERTICAL_PAGE_SPACING = 10.0;
    private const double DEFAULT_HORIZONTAL_PAGE_SPACING = 100.0;
    private const double DUAL_FRAME_DEFAULT_WIDTH_MULTIPLIER = 2.0;
    private const float FORCE_CONTINUOUS_ZOOM_THRESHOLD = 1.05F;
    private const double AUTO_SCROLL_PANNING_VELOCITY_MULTIPLIER_CONTINUOUS = 0.001;
    private const double AUTO_SCROLL_PANNING_VELOCITY_MULTIPLIER_SEPERATE = 0.0005;
    private const int AUTO_SCROLL_COMMON_SPEED = 20;
    private const int AUTO_SCROLL_COMMON_INTERVAL = 10000;
    private const double AUTO_SCROLL_DUAL_FRAME_MULTIPLIER = 1.8;
    private const double AUTO_SCROLL_COMMON_START_THRESHOLD = 0.1;
    private const double AUTO_SCROLL_COMMON_DEFAULT_VELOCITY = 0.05;

    #endregion

    #region Variables

    private bool _isLoaded = false;
    private bool _isDestoryed = false;
    private ReaderState _state = ReaderState.Idle;
    private bool _isVertical = true;
    private bool _isContinuous = true;
    private bool _isLeftToRight = true;
    private bool _useOriginalSize = false;
    private int _pageSpacing = 100;
    private readonly ReaderImageSettings _imageSettings = new();
    private bool _uiStateUpdatedOrientation = true;
    private bool _uiStateUpdatedContinuous = true;
    private bool _uiStateUpdatedFlowDirection = true;
    private bool _uiStateUpdatedNeedReload = true;
    private bool _uiStateUpdatedNeedReloadImages = true;
    private bool _postUiStateUpdated = false;

    private bool _isInitialFrameLoaded = false;
    private bool _isInitialFrameActionPerformed = false;
    private bool _isInitialFrameJumped = false;
    private bool _isFirstFrameLoaded = false;
    private bool _isLastFrameLoaded = false;

    private double _maxLinearVelocity = 0.0;
    private readonly UIElement _gestureReference;
    private readonly GestureHandler _gestureHandler;
    private readonly ReaderGestureRecognizer _gestureRecognizer = new();

    private readonly ITaskDispatcher _loadInfoDispatcher = TaskDispatcher.Factory.NewQueue("ReaderViewLoadInfoQueue");
    private ReaderViewDatabase? _internalDB = null;
    private IReadOnlyList<IImageSource> _originalDataModel = [];
    private double _initialPage = 1.0;

    private readonly CancellationSession _reloadSession;
    private readonly ObservableCollection<ReaderFrameViewModel> _frameItemsSource = [];
    private Action<int>? _frameReadyHandler;
    private IPageLayoutManager? _pendingPageLayoutManager = null;
    private IPageLayoutManager _pageLayoutManager = new SimplePageLayoutManager();
    private PageModel?[] _pageModels = [];
    private int _readyPageCount = 0;
    private double _minZoomFactor = double.MaxValue;
    private double _maxZoomFactor = double.MinValue;

    private IReadOnlyList<IReaderListViewItemViewModel> FrameItemsSource => _frameItemsSource;

    #endregion

    #region Constructor

    public ReaderView()
    {
        InitializeComponent();

        Loaded += OnLoadedOrUnloaded;
        Unloaded += OnLoadedOrUnloaded;

        _gestureReference = this;
        _gestureHandler = new(this);
        _gestureRecognizer.SetHandler(_gestureHandler);

        _reloadSession = new();
    }

    #endregion

    #region Public Interfaces

    public delegate void ReaderEventTappedEventHandler(ReaderView sender);
    public event ReaderEventTappedEventHandler? ReaderEventTapped;

    public delegate void ReaderEventPageChangedEventHandler(ReaderView sender, bool isIntermediate);
    public event ReaderEventPageChangedEventHandler? ReaderEventPageChanged;

    public delegate void ReaderEventReaderStateChangeHandler(ReaderView sender, ReaderState state, string description);
    public event ReaderEventReaderStateChangeHandler? ReaderEventReaderStateChanged;

    public delegate void ReaderEventZoomingChangedEventHandler(ReaderView sender, double zooming);
    public event ReaderEventZoomingChangedEventHandler? ReaderEventZoomingChanged;

    public delegate void ReaderEventAutoScrollingChangedEventHandler(ReaderView sender, bool isAutoScrolling);
    public event ReaderEventAutoScrollingChangedEventHandler? ReaderEventAutoScrollingChanged;

    public delegate void ReaderEventOverScrollEventHandler(ReaderView sender, bool forward);
    public event ReaderEventOverScrollEventHandler? ReaderEventOverScroll;

    public delegate Task<IReadOnlyList<BaseMenuFlyoutItemModel>> ImageContextRequestedCallback(ReaderView sender, ImageContextRequestedCallbackArgs args);
    public ImageContextRequestedCallback? ImageContextRequested { private get; set; }

    public int PageCount { get; private set; } = 0;
    public double CurrentPage { get; private set; } = 0.0;
    public int FrameCount => _frameItemsSource.Count;
    public int CurrentFrameIndex { get; private set; } = 0;
    public bool OverScrollEnabled { get; set; }

    private float _externalZooming = 1F;
    public float Zooming
    {
        get => _externalZooming;
        set
        {
            float fixedValue = Math.Max(0F, value);
            if (_externalZooming != fixedValue)
            {
                _externalZooming = fixedValue;
                SetScrollViewer2("SetZooming", ScrollSource.User,
                    zoom: fixedValue, disableAnimation: false);
                ReaderEventZoomingChanged?.Invoke(this, fixedValue);
            }
        }
    }

    public bool IsAutoScrolling
    {
        get => _isAutoScrolling;
        set
        {
            if (_isAutoScrolling == value)
            {
                return;
            }

            if (value)
            {
                StartAutoScrolling();
            }
            else
            {
                StopAutoScrolling();
            }
        }
    }

    public int GetFrameIndexByPage(int page)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(page, 0);

        int left = 0;
        int right = _frameItemsSource.Count - 1;

        while (left <= right)
        {
            int mid = left + (right - left) / 2;
            ReaderFrameViewModel frame = _frameItemsSource[mid];

            if (frame.PageL == page || frame.PageR == page)
            {
                return mid;
            }
            else if (frame.PageL < page)
            {
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }

        return -1;
    }

    public void SetIsVertical(bool isVertical)
    {
        if (isVertical == _isVertical)
        {
            return;
        }

        _isVertical = isVertical;
        _uiStateUpdatedOrientation = true;
        _uiStateUpdatedFlowDirection = true;
        UpdateUI();
    }

    public void SetIsContinuous(bool isContinuous)
    {
        if (isContinuous == _isContinuous)
        {
            return;
        }

        _isContinuous = isContinuous;
        _uiStateUpdatedContinuous = true;
        UpdateUI();
    }

    public void SetFlowDirection(bool isLeftToRight)
    {
        if (isLeftToRight == _isLeftToRight)
        {
            return;
        }

        _isLeftToRight = isLeftToRight;
        _uiStateUpdatedFlowDirection = true;
        UpdateUI();
    }

    public void SetPageLayoutManager(IPageLayoutManager manager)
    {
        if (manager.EquivalentTo(_pendingPageLayoutManager ?? _pageLayoutManager))
        {
            return;
        }

        _pendingPageLayoutManager = manager;
        _uiStateUpdatedNeedReload = true;
        UpdateUI();
    }

    public void SetUseOriginalSize(bool useOriginalSize)
    {
        if (useOriginalSize == _useOriginalSize)
        {
            return;
        }

        _useOriginalSize = useOriginalSize;
        _uiStateUpdatedNeedReload = true;
        UpdateUI();
    }

    public void SetPageSpacing(int pageSpacing)
    {
        if (pageSpacing == _pageSpacing)
        {
            return;
        }

        _pageSpacing = pageSpacing;
        _uiStateUpdatedNeedReload = true;
        UpdateUI();
    }

    public void SetImageRotation(ImageRotationEnum rotation)
    {
        if (rotation == _imageSettings.Rotation)
        {
            return;
        }

        _imageSettings.Rotation = rotation;
        _uiStateUpdatedNeedReload = true;
        UpdateUI();
    }

    public void SetImageFlip(bool flip)
    {
        if (flip == _imageSettings.Flip)
        {
            return;
        }

        _imageSettings.Flip = flip;
        _uiStateUpdatedNeedReloadImages = true;
        UpdateUI();
    }

    public void SetAntiAliasingFilter(double ratio)
    {
        ratio = Math.Abs(ratio);
        if (ratio == _imageSettings.AntiAliasingFilterRatio)
        {
            return;
        }

        _imageSettings.AntiAliasingFilterRatio = ratio;
        _uiStateUpdatedNeedReloadImages = true;
        UpdateUI();
    }

    public void SetImageBrightness(float brightness)
    {
        if (brightness == _imageSettings.Brightness)
        {
            return;
        }

        _imageSettings.Brightness = brightness;
        _uiStateUpdatedNeedReloadImages = true;
        UpdateUI();
    }

    public void SetImageContrast(float contrast)
    {
        if (contrast == _imageSettings.Contrast)
        {
            return;
        }

        _imageSettings.Contrast = contrast;
        _uiStateUpdatedNeedReloadImages = true;
        UpdateUI();
    }

    public void SetImageSaturation(float saturation)
    {
        if (saturation == _imageSettings.Saturation)
        {
            return;
        }

        _imageSettings.Saturation = saturation;
        _uiStateUpdatedNeedReloadImages = true;
        UpdateUI();
    }

    public void SetImageInvert(bool invert)
    {
        if (invert == _imageSettings.Invert)
        {
            return;
        }

        _imageSettings.Invert = invert;
        _uiStateUpdatedNeedReloadImages = true;
        UpdateUI();
    }

    public void SetInitialPage(double page)
    {
        _initialPage = Math.Max(0.5, page);
    }

    public void SetPage(double page)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);

        if (ComicLoaded)
        {
            SetScrollViewer2("SetPage", ScrollSource.UserPrecise, page: page);
        }
    }

    public void SetFrameIndex(int frameIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(frameIndex, _frameItemsSource.Count);

        if (ComicLoaded)
        {
            double page = _frameItemsSource[frameIndex].Page;
            SetScrollViewer2("SetFrameIndex", ScrollSource.UserPrecise, page: page);
        }
    }

    public void SetAutoScrollSpeed(int speed)
    {
        _autoScrollSpeed = Math.Max(0, speed);
    }

    public void SetConfigurationDatabase(IConfigurationDatabase? configDatabase)
    {
        _internalDB = configDatabase is null ? null : new(configDatabase);
    }

    public void Destory()
    {
        if (_isDestoryed)
        {
            return;
        }

        _isDestoryed = true;
        UpdateLoadedState();
        _reloadSession.Next();
        _frameItemsSource.Clear();
        ThisListView.MarkAsStopped();
    }

    public void StartLoadingImages(IEnumerable<IImageSource> images)
    {
        _originalDataModel = [.. images];
        Reload(_originalDataModel);
    }

    public void MoveFrame(int increment)
    {
        MoveFrameByUser("MoveFrame", increment);
    }

    #endregion

    #region UI

    private void UpdateUI()
    {
        if (!_postUiStateUpdated)
        {
            _postUiStateUpdated = true;
            PostToCurrentThread(delegate
            {
                _postUiStateUpdated = false;
                UpdateUIInternal();
            });
        }
    }

    private void UpdateUIInternal()
    {
        if (!_isLoaded)
        {
            return;
        }

        bool needReload = false;
        bool needReloadImages = false;

        if (_uiStateUpdatedOrientation)
        {
            _uiStateUpdatedOrientation = false;
            bool isVertical = _isVertical;
            ContentScrollViewer.VerticalScrollMode = isVertical ? ScrollMode.Enabled : ScrollMode.Disabled;
            ContentGrid.VerticalAlignment = isVertical ? VerticalAlignment.Top : VerticalAlignment.Center;
            ContentGrid.HorizontalAlignment = isVertical ? HorizontalAlignment.Center : HorizontalAlignment.Center;
            ContentListView.VerticalAlignment = isVertical ? VerticalAlignment.Top : VerticalAlignment.Center;
            ContentListView.HorizontalAlignment = isVertical ? HorizontalAlignment.Center : HorizontalAlignment.Center;
            ContentListView.Orientation = isVertical ? Orientation.Vertical : Orientation.Horizontal;

            needReload = true;
        }

        if (_uiStateUpdatedFlowDirection)
        {
            _uiStateUpdatedFlowDirection = false;
            FlowDirection oldFlowDirection = ContentScrollViewer.FlowDirection;
            if (_isVertical)
            {
                ContentScrollViewer.FlowDirection = FlowDirection.LeftToRight;
            }
            else
            {
                ContentScrollViewer.FlowDirection = _isLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            }

            needReload = needReload || oldFlowDirection != ContentScrollViewer.FlowDirection;
        }

        if (_uiStateUpdatedContinuous)
        {
            _uiStateUpdatedContinuous = false;
            _gestureRecognizer.AutoProcessInertia = _isContinuous;
        }

        if (_uiStateUpdatedNeedReload)
        {
            _uiStateUpdatedNeedReload = false;
            needReload = true;
        }

        if (_uiStateUpdatedNeedReloadImages)
        {
            _uiStateUpdatedNeedReloadImages = false;
            needReloadImages = true;
        }

        if (needReload)
        {
            // Overwrite initial page so that in not-first-loading scenario,
            // current page will remain unchanged after reloading
            if (_isInitialFrameJumped)
            {
                _initialPage = CurrentPage;
            }

            Reload(_originalDataModel);
        }
        else if (needReloadImages)
        {
            UpdateImages("UIStateUpdatedNeedReloadImages", redraw: true);
        }
    }

    private void UpdateMinMaxZoomFactor(int frameIndex)
    {
        ZoomCoefficient? zoomCoefficient = CalculateZoomCoefficient(frameIndex);
        if (zoomCoefficient is null)
        {
            return;
        }

        double maxZoomFactor = MAX_ZOOM * zoomCoefficient.Max();
        double minZoomFactor = Math.Min(MIN_ZOOM_CENTER_INSIDE * zoomCoefficient.Min(), MIN_ZOOM_CENTER_CROP * zoomCoefficient.Max());
        _maxZoomFactor = Math.Max(_maxZoomFactor, maxZoomFactor);
        _minZoomFactor = Math.Min(_minZoomFactor, minZoomFactor);
    }

    private bool UpdatePage()
    {
        if (!_isInitialFrameLoaded || _frameItemsSource.Count == 0)
        {
            return false;
        }

        double offset;
        {
            double parallelOffset = SCParallelOffsetFinal;
            double zoomFactor = SCZoomFactorFinal;
            offset = (parallelOffset + ViewportParallelLength * 0.5) / zoomFactor;
        }

        // Locate nearest frames using binary search
        int lo = 0;
        int hi = _frameItemsSource.Count;
        while (hi - lo > 2)
        {
            int i = (lo + hi) / 2;
            FrameOffsetData? offsets = FrameOffset(i);
            if (!offsets.HasValue)
            {
                hi = i;
                continue;
            }

            if ((offsets.Value.ParallelStart + offsets.Value.ParallelEnd) * 0.5 < offset)
            {
                lo = i;
            }
            else
            {
                hi = i + 1;
            }
        }

        AnchorConverter? converter = CreateAnchorConverter(lo, hi);
        if (converter is null)
        {
            return false;
        }

        double page = converter.CalculatePage(offset);
        page = Math.Min(page, PageCount + 0.5);
        int discretePage = ToDiscretePage(page);
        if (!TryConvertPageToFrameIndex(discretePage, out int frameIndex))
        {
            return false;
        }

        CurrentPage = page;
        CurrentFrameIndex = frameIndex;
        return true;
    }

    private void UpdateImages(string reason, bool redraw = false)
    {
        if (!ComicLoaded)
        {
            return;
        }

        int frame = SCCurrentFrameIndexFinal;
        if (frame < 0 || frame >= _frameItemsSource.Count)
        {
            return;
        }

        Log("LoadImage", $"Reason={reason}", $"F={frame}");

        List<int> frameIndices = [];
        int maxPreloadPagesAfter = AppSettingsModel.Instance.PreloadPagesAfter;
        int maxPreloadPagesBefore = AppSettingsModel.Instance.PreloadPagesBefore;
        int preloadedPagesAfter = 0;
        int preloadedPagesBefore = 0;

        for (int i = 0; i < _frameItemsSource.Count; i++)
        {
            if (i == 0)
            {
                frameIndices.Add(frame);
                continue;
            }

            int frameAfter = frame + i;
            if (frameAfter >= 0 && frameAfter < _frameItemsSource.Count)
            {
                ReaderFrameViewModel model = _frameItemsSource[frameAfter];
                if (preloadedPagesAfter < maxPreloadPagesAfter)
                {
                    frameIndices.Add(frameAfter);
                    preloadedPagesAfter += model.PageCount;
                }
            }

            int frameBefore = frame - i;
            if (frameBefore >= 0 && frameBefore < _frameItemsSource.Count)
            {
                ReaderFrameViewModel model = _frameItemsSource[frameBefore];
                if (preloadedPagesBefore < maxPreloadPagesBefore)
                {
                    frameIndices.Add(frameBefore);
                    preloadedPagesBefore += model.PageCount;
                }
            }
        }

        ThisListView.SetVisibleItemIndices(frameIndices);

        double rasterizationScale = DisplayUtils.GetRasterizationScale(this);
        double imageScale = SCZoomFactorFinal / _imageSettings.AntiAliasingFilterRatio * rasterizationScale;

        foreach (int i in frameIndices)
        {
            ReaderFrameViewModel model = _frameItemsSource[i];
            model.SetScale(imageScale);

            if (redraw)
            {
                model.RedrawImage();
            }
        }
    }

    #endregion

    #region Loader

    private double InitialPage => Math.Min(_initialPage, PageCount + 0.5);
    private bool ComicLoaded => _isLoaded && PageCount > 0;

    private void Reload(IReadOnlyList<IImageSource> images)
    {
        if (images.Count == 0 || _isDestoryed)
        {
            return;
        }

        // Refresh token
        _reloadSession.Next();
        CancellationSession.IToken token = _reloadSession.Token;

        // Reset visible frames
        ThisListView.SetVisibleItemIndices([]);

        // Reset internal states
        PageCount = images.Count;
        _pageModels = new PageModel?[PageCount];
        _readyPageCount = 0;
        CurrentPage = 1;
        CurrentFrameIndex = 0;
        _minZoomFactor = double.MaxValue;
        _maxZoomFactor = double.MinValue;
        _isPreciseScrolling = false;
        SCClearFinalVal("Reload");
        Log("Reload", $"IP={InitialPage}", $"LP={PageCount}");

        // Reset loader
        Log("Load", "Reset");
        _isInitialFrameLoaded = false;
        _isInitialFrameActionPerformed = false;
        _isInitialFrameJumped = false;
        _isFirstFrameLoaded = false;
        _isLastFrameLoaded = false;

        // Reset page layout manager
        if (_pendingPageLayoutManager is not null)
        {
            // The actual instance can only be replaced here to guarantee the calling order
            _pageLayoutManager = _pendingPageLayoutManager;
            _pendingPageLayoutManager = null;
        }

        _pageLayoutManager.Reset(PageCount);

        // Use upper bound of initial page to ensure the frame info for initial jump is loaded
        int initialPageUpperBound = Math.Clamp((int)Math.Ceiling(InitialPage), 1, PageCount);

        _frameReadyHandler = (index) =>
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (index < 0 || index >= _frameItemsSource.Count)
            {
                Logger.F(TAG, $"Invalid frame index {index} in ready handler");
                return;
            }

            ReaderFrameViewModel frame = _frameItemsSource[index];
            if (frame.MaxPage == ReaderFrameViewModel.NO_PAGE)
            {
                Logger.F(TAG, $"Invalid max page for frame index {index} in ready handler");
                return;
            }

            if (!_isInitialFrameLoaded && (frame.PageL == initialPageUpperBound || frame.PageR == initialPageUpperBound))
            {
                _isInitialFrameLoaded = true;
            }

            if (index == 0)
            {
                _isFirstFrameLoaded = true;
            }

            if (!_isLastFrameLoaded && frame.MaxPage == PageCount)
            {
                _isLastFrameLoaded = true;
            }

            UpdateLoader($"FrameReady,i={index}");

            int progress = Math.Min(99, (int)(frame.MaxPage * 100.0 / PageCount));
            DispatchReaderStateChangeEvent(_state, $"{StringResourceProvider.Instance.ReaderStatusLoading} ({progress}%)");
        };

        // Start loading frames
        DispatchReaderStateChangeEvent(ReaderState.Loading, StringResourceProvider.Instance.ReaderStatusLoading);

        _frameItemsSource.Clear();

        _loadInfoDispatcher.SubmitAsync(async () =>
        {
            void dispatchToMainThread(List<PengingImageItem> pendingList)
            {
                if (pendingList.Count == 0)
                {
                    return;
                }

                List<PengingImageItem> pendingListCopy = [.. pendingList];
                pendingList.Clear();
                CoroutineUtils.RunInMainThread(() =>
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    foreach (PengingImageItem item in pendingListCopy)
                    {
                        AddPage(item.Page, item.OriginalWidth, item.OriginalHeight, item.Source);
                    }
                });
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            List<PengingImageItem> pendingList = [];
            for (int i = 0; i < images.Count; i++)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                IImageSource image = images[i];
                ImageMeta? meta = await ImageLoader.LoadImageMeta(image, new()
                {
                    Priority = ImageLoadingPriority.READER_IMAGE,
                });

                int width = 0;
                int height = 0;
                if (meta is not null)
                {
                    width = meta.Width;
                    height = meta.Height;
                }

                pendingList.Add(new()
                {
                    Page = i + 1,
                    OriginalWidth = width,
                    OriginalHeight = height,
                    Source = image,
                });

                if (stopwatch.LapSpan().TotalMilliseconds > 500)
                {
                    stopwatch.Lap();
                    dispatchToMainThread(pendingList);
                }
            }

            dispatchToMainThread(pendingList);
        });
    }

    private void AddPage(int page, int originalWidth, int originalHeight, IImageSource source)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page, nameof(page));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(page, PageCount, nameof(page));

        ReaderImageSource imageSourceModel = new()
        {
            Source = source,
            Settings = _imageSettings,
        };
        int imageWidth = imageSourceModel.Settings.Rotation switch
        {
            ImageRotationEnum.Rotate90 or ImageRotationEnum.Rotate270 => originalHeight,
            _ => originalWidth,
        };
        int imageHeight = imageSourceModel.Settings.Rotation switch
        {
            ImageRotationEnum.Rotate90 or ImageRotationEnum.Rotate270 => originalWidth,
            _ => originalHeight,
        };

        PageModel pageModel = new()
        {
            Image = imageSourceModel,
            OriginalWidth = imageWidth,
            OriginalHeight = imageHeight,
        };

        if (_pageModels[page - 1] is not null)
        {
            throw new InvalidOperationException($"Page {page} has already been added.");
        }

        _pageModels[page - 1] = pageModel;
        _pageLayoutManager.AddPage(page, originalWidth, originalHeight);
        IncreaseReadyPageIndex(page == PageCount);
    }

    private void IncreaseReadyPageIndex(bool assertCompletion)
    {
        int readyFrameCount = _frameItemsSource.Count;

        for (int page = _readyPageCount + 1; page <= PageCount; page++)
        {
            PageModel? pageModel = _pageModels[page - 1];
            if (pageModel is null)
            {
                break;
            }

            PageLayoutInfo? pageLayout = pageModel.LayoutInfo;
            if (pageLayout is null)
            {
                if (!_pageLayoutManager.TryGetPageLayout(page, out pageLayout))
                {
                    if (assertCompletion)
                    {
                        Logger.F(TAG, $"Failed to acquire layout for page {page}");
                    }

                    break;
                }

                pageModel.LayoutInfo = pageLayout;
            }

            int frameIndex = pageLayout.FrameIndex;
            if (frameIndex < _frameItemsSource.Count)
            {
                continue;
            }

            if (frameIndex != _frameItemsSource.Count)
            {
                Logger.F(TAG, $"Expect frame {_frameItemsSource.Count}, get frame {frameIndex}");
                break;
            }

            bool isDoubleWidth;
            bool isLeftSide;
            switch (pageLayout.LayoutType)
            {
                case PageLayoutType.Single:
                    isLeftSide = true;
                    isDoubleWidth = false;
                    break;
                case PageLayoutType.Spread:
                    isLeftSide = true;
                    isDoubleWidth = true;
                    break;
                case PageLayoutType.Left:
                    isLeftSide = true;
                    isDoubleWidth = true;
                    break;
                case PageLayoutType.Right:
                    isLeftSide = false;
                    isDoubleWidth = true;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown layout type '{pageLayout.LayoutType}'.");
            }

            int neighbour = pageLayout.NeighbourPage;
            PageModel? neighbourModel = null;
            if (neighbour != ReaderFrameViewModel.NO_PAGE)
            {
                neighbourModel = _pageModels[neighbour - 1];
                if (neighbourModel is null)
                {
                    break;
                }
            }

            double verticalPadding = DEFAULT_VERTICAL_PAGE_SPACING;
            double horizontalPadding = DEFAULT_HORIZONTAL_PAGE_SPACING;
            verticalPadding = _isVertical ? verticalPadding : 0;
            horizontalPadding = _isVertical ? 0 : horizontalPadding;
            verticalPadding *= _pageSpacing / 100.0;
            horizontalPadding *= _pageSpacing / 100.0;

            double thisImageWidth = 0;
            double thisImageHeight = 0;
            double neighbourImageWidth = 0;
            double neighbourImageHeight = 0;
            if (_useOriginalSize)
            {
                double totalWidth = pageModel.OriginalWidth;
                double maxHeight = pageModel.OriginalHeight;
                if (neighbourModel is not null)
                {
                    totalWidth += neighbourModel.OriginalWidth;
                    maxHeight = Math.Max(maxHeight, neighbourModel.OriginalHeight);
                }

                if (totalWidth < 1 || maxHeight < 1)
                {
                    verticalPadding = 0;
                    horizontalPadding = 0;
                }
                else
                {
                    thisImageWidth = pageModel.OriginalWidth;
                    thisImageHeight = pageModel.OriginalHeight;
                    if (neighbourModel is not null)
                    {
                        neighbourImageWidth = neighbourModel.OriginalWidth;
                        neighbourImageHeight = neighbourModel.OriginalHeight;
                    }
                }
            }
            else
            {
                double aspectRatio = pageModel.AspectRatio;
                if (neighbourModel is not null)
                {
                    aspectRatio += neighbourModel.AspectRatio;
                }

                if (aspectRatio < 1e-3)
                {
                    verticalPadding = 0;
                    horizontalPadding = 0;
                }
                else
                {
                    double defaultWidth = DEFAULT_IMAGE_SIDE_LENGTH;
                    double defaultHeight = DEFAULT_IMAGE_SIDE_LENGTH;
                    if (isDoubleWidth)
                    {
                        defaultWidth *= DUAL_FRAME_DEFAULT_WIDTH_MULTIPLIER;
                    }

                    thisImageHeight = _isVertical ? defaultWidth / aspectRatio : defaultHeight;
                    thisImageWidth = thisImageHeight * pageModel.AspectRatio;
                    if (neighbourModel is not null)
                    {
                        neighbourImageHeight = thisImageHeight;
                        neighbourImageWidth = thisImageHeight * neighbourModel.AspectRatio;
                    }
                }
            }

            Logger.Assert(double.IsFinite(thisImageWidth), $"Invalid image width {thisImageWidth}");
            Logger.Assert(double.IsFinite(thisImageHeight), $"Invalid image height {thisImageHeight}");
            Logger.Assert(double.IsFinite(neighbourImageWidth), $"Invalid neighbour image width {neighbourImageWidth}");
            Logger.Assert(double.IsFinite(neighbourImageHeight), $"Invalid neighbour image height {neighbourImageHeight}");
            Logger.Assert(double.IsFinite(horizontalPadding), "B742A59FA82023CD");
            Logger.Assert(double.IsFinite(verticalPadding), "37E400F20758C487");

            bool isFirstFrame = frameIndex == 0;
            bool isLastFrame = pageLayout.IsLastFrame;
            double topPadding = _isVertical && isFirstFrame ? 10000 : verticalPadding;
            double bottomPadding = _isVertical && isLastFrame ? 10000 : verticalPadding;
            double startPadding = !_isVertical && isFirstFrame ? 10000 : horizontalPadding;
            double endPadding = !_isVertical && isLastFrame ? 10000 : horizontalPadding;

            Thickness frameMargin = new(startPadding, topPadding, endPadding, bottomPadding);

            double leftImageWidth;
            double leftImageHeight;
            double rightImageWidth;
            double rightImageHeight;
            int pageL;
            int pageR;

            if (isLeftSide)
            {
                leftImageWidth = thisImageWidth;
                leftImageHeight = thisImageHeight;
                rightImageWidth = neighbourImageWidth;
                rightImageHeight = neighbourImageHeight;
                pageL = page;
                pageR = neighbour;
            }
            else
            {
                leftImageWidth = neighbourImageWidth;
                leftImageHeight = neighbourImageHeight;
                rightImageWidth = thisImageWidth;
                rightImageHeight = thisImageHeight;
                pageR = page;
                pageL = neighbour;
            }

            ReaderImageSource? leftImageSource = null;
            ReaderImageSource? rightImageSource = null;

            if (pageL != ReaderFrameViewModel.NO_PAGE)
            {
                PageModel? leftImageModel = _pageModels[pageL - 1];
                if (leftImageModel is not null)
                {
                    leftImageSource = leftImageModel.Image;
                }
                else
                {
                    Logger.F(TAG, "Left image model is null.");
                }
            }

            if (pageR != ReaderFrameViewModel.NO_PAGE)
            {
                PageModel? rightImageModel = _pageModels[pageR - 1];
                if (rightImageModel is not null)
                {
                    rightImageSource = rightImageModel.Image;
                }
                else
                {
                    Logger.F(TAG, "Right image model is null.");
                }
            }

            async Task<IReadOnlyList<BaseMenuFlyoutItemModel>> RequestImageContextMenu(int imageIndex)
            {
                ImageContextRequestedCallback? callback = ImageContextRequested;
                if (callback is null)
                {
                    return [];
                }

                IImageSource? image = null;
                int page;

                if (imageIndex == 0)
                {
                    image = leftImageSource?.Source;
                    page = pageL;
                }
                else if (imageIndex == 1)
                {
                    image = rightImageSource?.Source;
                    page = pageR;
                }
                else
                {
                    return [];
                }

                if (image is null)
                {
                    return [];
                }

                return await callback(this, new()
                {
                    ImageIndex = page - 1,
                    Image = image,
                });
            }

            ReaderFrameViewModel item = new()
            {
                FrameMargin = frameMargin,
                LeftImageSource = leftImageSource,
                RightImageSource = rightImageSource,
                LeftImageWidth = leftImageWidth,
                LeftImageHeight = leftImageHeight,
                RightImageWidth = rightImageWidth,
                RightImageHeight = rightImageHeight,
                PageL = pageL,
                PageR = pageR,
                RequestImageContextMenu = RequestImageContextMenu,
            };

            _frameItemsSource.Add(item);
            _readyPageCount = page;
            UpdateMinMaxZoomFactor(frameIndex);
        }

        for (int i = readyFrameCount; i < _frameItemsSource.Count; i++)
        {
            _frameReadyHandler?.Invoke(i);
        }
    }

    private void UpdateLoader(string reason)
    {
        if (!_isLoaded)
        {
            return;
        }

        Log("Load", "Start", $"Reason={reason}");

        bool needDispatchReadyState = false;

        if (_isInitialFrameLoaded && !_isInitialFrameActionPerformed)
        {
            Log("Load", "InitialFrame");
            _isInitialFrameActionPerformed = true;

            LoadZoomingConfig(out float zoom, out ZoomType zoomType);

            ScrollResult scrollResult = SetScrollViewer2(
                "JumpToInitialPage",
                ScrollSource.Programmatic,
                zoom: zoom,
                zoomType: zoomType,
                page: InitialPage);
            Log("Load", "InitialFrameScroll", $"Result={scrollResult}");

            if (scrollResult == ScrollResult.Unchanged)
            {
                OnViewChanged(false);
            }
            else
            {
                EnsureInitialPageJumped();
            }

            UpdateImages("InitialFrameLoaded");

            needDispatchReadyState = true;
        }

        if (needDispatchReadyState)
        {
            DispatchReaderStateChangeEvent(ReaderState.Ready);
        }
    }

    private void EnsureInitialPageJumped()
    {
        // In some weird cases, ChangeView method completes successfully,
        // but neither the actual offset has changed or ViewChange callback
        // is being triggered.
        // Possible reproducing path: Switch from vertical view to horizontal view.
        // We check the flag periodically to ensure offset has actually changed.
        // If not, try set the offset again.

        CancellationSession.IToken token = _reloadSession.Token;
        CoroutineUtils.Run(async () =>
        {
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(100);

                if (token.IsCancellationRequested)
                {
                    break;
                }

                if (_isInitialFrameJumped)
                {
                    break;
                }

                ScrollResult scrollResult = SetScrollViewer3(
                    $"JumpToInitialPageRetry{i}",
                    ScrollSource.Programmatic);
                Log("Load", $"InitialFrameScrollRetry{i}", $"Result={scrollResult}");

                if (scrollResult == ScrollResult.Unchanged)
                {
                    OnViewChanged(false);
                    break;
                }
            }
        });
    }

    #endregion

    #region Load/Unload Handlers

    private void OnLoadedOrUnloaded(object sender, RoutedEventArgs e)
    {
        UpdateLoadedState();
    }

    private void OnReaderListViewLoadedOrUnloaded(object sender, RoutedEventArgs e)
    {
        UpdateLoadedState();
    }

    private void OnReaderScrollViewerLoadedOrUnloaded(object sender, RoutedEventArgs e)
    {
        UpdateLoadedState();
    }

    private void UpdateLoadedState()
    {
        bool viewLoaded = IsLoaded;
        bool lvLoaded = ContentListView != null && ContentListView.IsLoaded;
        bool svLoaded = ContentScrollViewer != null && ContentScrollViewer.IsLoaded;
        bool isLoaded = viewLoaded && lvLoaded && svLoaded && !_isDestoryed;

        if (_isLoaded == isLoaded)
        {
            return;
        }

        _isLoaded = isLoaded;
        if (isLoaded)
        {
            UpdateUI();
            UpdateLoader("Loaded");
            UpdateImages("Loaded");
        }
        else
        {
            DisposeCursor();
        }
    }

    #endregion

    #region Size Change Event Handlers

    private void OnReaderScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!ComicLoaded)
        {
            return;
        }

        Log("SizeChanged",
            $"OS=({e.PreviousSize})",
            $"NS=({e.NewSize})",
            $"ZF={ZoomFactor}",
            $"H={HorizontalOffset}",
            $"V={VerticalOffset}");

        SetScrollViewer2($"SizeChanged", ScrollSource.Programmatic, zoom: _zoom, page: CurrentPage);
    }

    #endregion

    #region Scroll Event Handlers

    private bool _isViewChanging = false;
    private long _lastFinalViewChangeTicks = 0;

    private void OnReaderScrollViewerViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        OnViewChanged(e.IsIntermediate);
    }

    private void OnViewChanged(bool isIntermediate)
    {
        if (_isCommitting)
        {
            Log("ViewChanged", "IgnoreCommitting");
            return;
        }

        bool final = !isIntermediate;
        if (_isAutoScrolling && _isContinuous && GetTicks() - _lastFinalViewChangeTicks < 500)
        {
            final = false;
        }

        if (final)
        {
            Log("ViewChanged",
                $"Z={ZoomFactor}",
                $"H={HorizontalOffset}",
                $"V={VerticalOffset}");
        }

        if (!_isInitialFrameJumped)
        {
            if (!_isInitialFrameActionPerformed)
            {
                return;
            }

            double parallelDiff = ParallelOffset - SCParallelOffsetFinal;
            float zoomDiff = ZoomFactor / SCZoomFactorFinal - 1F;
            if (Math.Abs(parallelDiff) < 10 && Math.Abs(zoomDiff) < 0.05)
            {
                Log("ViewChanged", $"InitialFrameJumped", $"PD={parallelDiff}", $"ZD={zoomDiff}");
                _isInitialFrameJumped = true;
            }
            else
            {
                return;
            }
        }

        if (final)
        {
            _lastFinalViewChangeTicks = GetTicks();
        }

        _isViewChanging = true;
        try
        {
            OnViewChangedInternal(final);
        }
        finally
        {
            _isViewChanging = false;
        }
    }

    private void OnViewChangedInternal(bool final)
    {
        if (!UpdatePage())
        {
            return;
        }

        if (final)
        {
            SCClearFinalVal("ViewChanged");

            // Notify the scroll viewer to update its inner states.
            SetScrollViewer1("AdjustInnerStateAfterViewChanged", ScrollSource.Programmatic, disableAnimation: false);

            if (_zoom < FORCE_CONTINUOUS_ZOOM_THRESHOLD)
            {
                int frame = SCCurrentFrameIndexFinal;
                frame = Math.Max(0, Math.Min(_frameItemsSource.Count - 1, frame));
                if (frame < _frameItemsSource.Count)
                {
                    double page = _frameItemsSource[frame].Page;
                    if (_isContinuous)
                    {
                        // Stick to the vertical center of current frame.
                        SetScrollViewer2("StickToVerticalCenter", ScrollSource.Programmatic,
                            page: page, applyParallelOffset: false, disableAnimation: false);
                    }
                    else if (!_isPreciseScrolling)
                    {
                        // Stick to the center of current frame.
                        SetScrollViewer2("StickToFrameCenter", ScrollSource.Programmatic,
                            page: page, disableAnimation: false);
                    }
                }
            }

            UpdateImages("ViewChanged");
            SaveZoomingConfig();
        }

        ReaderEventPageChanged?.Invoke(this, !final);

        if (_zoom != _externalZooming)
        {
            _externalZooming = _zoom;
            ReaderEventZoomingChanged?.Invoke(this, _externalZooming);
        }
    }

    #endregion

    #region Pointer Event Handlers

    private bool _pointerDown = false;
    private bool _isInInertiaTranslation = false;
    private PointerPoint? _initiatePointerPoint;

    private void OnReaderPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        ((UIElement)sender).CapturePointer(e.Pointer);
        OnReaderPointerEvent(PointerEventType.Pressed, e);
    }

    private void OnReaderPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && AppSettingsModel.Instance.AutoHideCursor)
        {
            ShowCursor();
            HideCursorDelayed(3000);
        }

        OnReaderPointerEvent(PointerEventType.Moved, e);
    }

    private void OnReaderPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        ((UIElement)sender).ReleasePointerCapture(e.Pointer);
        OnReaderPointerEvent(PointerEventType.Released, e);
    }

    private void OnReaderPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        ((UIElement)sender).ReleasePointerCapture(e.Pointer);
        OnReaderPointerEvent(PointerEventType.Cancelled, e);
    }

    private void OnReaderPointerEvent(PointerEventType type, PointerRoutedEventArgs e)
    {
        PointerPoint pointerPoint = e.GetCurrentPoint(_gestureReference);
        switch (type)
        {
            case PointerEventType.Pressed:
                _pointerDown = true;
                _initiatePointerPoint = pointerPoint;
                if (pointerPoint.Properties.IsMiddleButtonPressed)
                {
                    StopMiddleButtonAutoScrolling();
                    StartMiddleButtonAutoScrolling(pointerPoint.Position);
                }
                else if (_isMiddleButtonAutoScrolling)
                {
                    _initiatePointerPoint = null; // Suppress future events
                    StopMiddleButtonAutoScrolling();
                }
                break;

            case PointerEventType.Moved:
                UpdateMiddleButtonAutoScrolling(pointerPoint.Position);
                break;

            case PointerEventType.Released:
            case PointerEventType.Cancelled:
                _pointerDown = false;
                break;

            default:
                break;
        }

        PointerPoint? initiatePointerPoint = _initiatePointerPoint;
        if (initiatePointerPoint is null)
        {
            return;
        }

        if (!initiatePointerPoint.Properties.IsMiddleButtonPressed)
        {
            switch (type)
            {
                case PointerEventType.Pressed:
                    _gestureRecognizer.ProcessDownEvent(pointerPoint);
                    break;
                case PointerEventType.Moved:
                    {
                        IList<PointerPoint> points = e.GetIntermediatePoints(_gestureReference);
                        _gestureRecognizer.ProcessMoveEvents(points);
                    }
                    break;
                case PointerEventType.Released:
                case PointerEventType.Cancelled:
                    _gestureRecognizer.ProcessUpEvent(pointerPoint);
                    if (!_gestureRecognizer.AutoProcessInertia)
                    {
                        _gestureRecognizer.CompleteGesture();
                    }
                    break;
                default:
                    break;
            }
        }

        switch (type)
        {
            case PointerEventType.Pressed:
            case PointerEventType.Moved:
                break;
            case PointerEventType.Released:
            case PointerEventType.Cancelled:
                _initiatePointerPoint = null;
                break;
            default:
                break;
        }
    }

    private void OnReaderScrollViewerPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        // Ctrl key down indicates the user is zooming. In that case we shouldn't handle this event.
        Windows.UI.Core.CoreVirtualKeyStates ctrlState = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
        if (ctrlState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
        {
            return;
        }

        OnReaderScrollViewerPointerWheelChanged(e);
    }

    private void OnReaderManipulationStarted(object sender, ManipulationStartedEventArgs e)
    {
        _isInInertiaTranslation = false;
        _maxLinearVelocity = 0.0;
    }

    private void OnReaderManipulationUpdated(object sender, ManipulationUpdatedEventArgs e)
    {
        bool inertia = !_pointerDown;
        _isInInertiaTranslation = inertia;

        double dx = e.Delta.Translation.X;
        double dy = e.Delta.Translation.Y;
        float scale = e.Delta.Scale;

        if (!_isVertical && !_isLeftToRight)
        {
            dx = -dx;
        }

        float? zoom = null;

        if (Math.Abs(scale - 1.0F) > 0.01F)
        {
            zoom = _zoom * scale;
        }

        SetScrollViewer3("ContinuousScrollingUsingManipulation", ScrollSource.User, zoom: zoom,
            horizontalOffset: SCHorizontalOffsetFinal - dx, verticalOffset: SCVerticalOffsetFinal - dy, disableAnimation: false);

        // Handle auto scrolling in continuous mode
        double v = _isVertical ? e.Velocities.Linear.Y : e.Velocities.Linear.X;
        _maxLinearVelocity = Math.Max(_maxLinearVelocity, Math.Abs(v));
        bool forwardDirection = (_isVertical || _isLeftToRight) ? double.IsNegative(v) : double.IsPositive(v);
        if (IsAutoScrollEnabled && _isContinuous && inertia && forwardDirection)
        {
            double threshold = _maxLinearVelocity * AUTO_SCROLL_COMMON_START_THRESHOLD * _autoScrollSpeed / AUTO_SCROLL_COMMON_SPEED;
            if (Math.Abs(v) < threshold)
            {
                _gestureRecognizer.CompleteGesture();
                StartAutoScrolling(velocity: threshold);
            }
        }
    }

    private void OnReaderManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
    {
        _isInInertiaTranslation = false;

        if (!_isContinuous && _zoom < FORCE_CONTINUOUS_ZOOM_THRESHOLD)
        {
            double velocity = _isVertical ? e.Velocities.Linear.Y : e.Velocities.Linear.X;

            if (!_isVertical && !_isLeftToRight)
            {
                velocity = -velocity;
            }

            if (velocity > 1.0)
            {
                MoveFrameByUser("MoveToLastPageUsingManipulation", -1);
            }
            else if (velocity < -1.0)
            {
                MoveFrameByUser("MoveToNextPageUsingManipulation", 1);

                // Page turning in seperate mode starts auto scrolling
                StartAutoScrolling();
            }
        }
    }

    private long _lastTouchpadPageTurnTicks = 0;

    private void OnReaderScrollViewerPointerWheelChanged(PointerRoutedEventArgs e)
    {
        PointerPoint pt = e.GetCurrentPoint(null);
        int delta = -pt.Properties.MouseWheelDelta;
        bool isHorizontal = pt.Properties.IsHorizontalMouseWheel;
        Log("PointerWheelChanged", $"Delta={delta}", $"Horizontal={isHorizontal}");

        if (isHorizontal && (_isVertical || _isLeftToRight))
        {
            delta = -delta;
        }

        if (_isContinuous || _zoom > FORCE_CONTINUOUS_ZOOM_THRESHOLD)
        {
            Windows.UI.Core.CoreVirtualKeyStates menuState = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu);
            bool altDown = menuState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            bool verticalScrolling = !isHorizontal;

            if (!_isVertical)
            {
                int frame = SCCurrentFrameIndexFinal;
                ZoomCoefficient? zoomCoefficient = CalculateZoomCoefficient(frame);
                if (zoomCoefficient != null)
                {
                    double zoomFitHeight = SCZoomFactorFinal / zoomCoefficient.FitHeight;
                    if (zoomFitHeight <= FORCE_CONTINUOUS_ZOOM_THRESHOLD)
                    {
                        verticalScrolling = false;
                    }
                }
                else
                {
                    verticalScrolling = false;
                }
            }

            verticalScrolling = verticalScrolling != altDown;

            double movement = (double)delta / Windows.Win32.PInvoke.WHEEL_DELTA * 140.0;
            if (verticalScrolling)
            {
                SetScrollViewer3("ContinuousVerticalScrollingUsingPointerWheel", ScrollSource.User,
                    verticalOffset: SCVerticalOffsetFinal + movement, disableAnimation: false);
            }
            else
            {
                SetScrollViewer3("ContinuousHorizontalScrollingUsingPointerWheel", ScrollSource.User,
                    horizontalOffset: SCHorizontalOffsetFinal + movement, disableAnimation: false);
            }
        }
        else
        {
            // Touchpad support is experimental since for now there is no way to reliablely distinguish touchpad and mouse wheel.
            bool isTouchpad = pt.PointerDeviceType == PointerDeviceType.Touchpad || delta % (int)Windows.Win32.PInvoke.WHEEL_DELTA != 0;

            long nowTicks = GetTicks();
            if (nowTicks - _lastTouchpadPageTurnTicks < 200)
            {
                // Suppress any page turn events since we can't tell whether the event is from touchpad or mouse wheel,
                // but we know for sure that it's not from mouse wheel if it's too frequent.
                if (isTouchpad)
                {
                    _lastTouchpadPageTurnTicks = nowTicks;
                }
            }
            else
            {
                int movement = delta / (int)Windows.Win32.PInvoke.WHEEL_DELTA;
                if (isTouchpad)
                {
                    movement = Math.Sign(movement);
                    if (movement != 0)
                    {
                        _lastTouchpadPageTurnTicks = nowTicks;
                        MoveFrameByUser("PageTurningUsingTouchpadWheel", movement);
                    }
                }
                else
                {
                    MoveFrameByUser("PageTurningUsingPointerWheel", movement);
                }
            }
        }

        e.Handled = true;
    }

    private bool _tapPending = false;
    private bool _tapCancelled = false;

    private void OnReaderTapped(object sender, TappedEventArgs e)
    {
        if (e.TapCount == 1)
        {
            if (_tapPending)
            {
                return;
            }

            _tapPending = true;
            _tapCancelled = false;
            CoroutineUtils.Run(async () =>
            {
                await Task.Delay(100);
                _tapPending = false;
                if (_tapCancelled)
                {
                    return;
                }

                ReaderEventTapped?.Invoke(this);
            });
        }
        else if (e.TapCount == 2)
        {
            _tapCancelled = true;
            if (Math.Abs(_zoom - 1F) <= 0.01F)
            {
                SetScrollViewer3("FitScreenUsingCenterCrop", ScrollSource.User, zoom: 1F, zoomType: ZoomType.CenterCrop, disableAnimation: false);
            }
            else
            {
                SetScrollViewer3("FitScreenUsingCenterCrop", ScrollSource.User, zoom: 1F, zoomType: ZoomType.CenterInside, disableAnimation: false);
            }
        }
    }

    #endregion

    #region Key Down Event Handlers

    private void OnReaderKeyDown(object sender, KeyRoutedEventArgs e)
    {
        bool handled = true;
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Right:
                if (_isLeftToRight)
                {
                    MoveFrameByUser("JumpToNextPageUsingRightKey", 1);
                }
                else
                {
                    MoveFrameByUser("JumpToPreviousPageUsingRightKey", -1);
                }

                break;

            case Windows.System.VirtualKey.Left:
                if (_isLeftToRight)
                {
                    MoveFrameByUser("JumpToPreviousPageUsingLeftKey", -1);
                }
                else
                {
                    MoveFrameByUser("JumpToNextPageUsingLeftKey", 1);
                }

                break;

            case Windows.System.VirtualKey.Up:
                MoveFrameByUser("JumpToPerviousPageUsingUpKey", -1);
                break;

            case Windows.System.VirtualKey.Down:
                MoveFrameByUser("JumpToNextPageUsingDownKey", 1);
                break;

            case Windows.System.VirtualKey.PageUp:
                MoveFrameByUser("JumpToPerviousPageUsingPgUpKey", -1);
                break;

            case Windows.System.VirtualKey.PageDown:
                MoveFrameByUser("JumpToNextPageUsingPgDownKey", 1);
                break;

            case Windows.System.VirtualKey.Home:
                SetScrollViewer2("JumpToFirstPageUsingHomeKey", ScrollSource.User, page: 1);
                break;

            case Windows.System.VirtualKey.End:
                SetScrollViewer2("JumpToLastPageUsingEndKey", ScrollSource.User, page: PageCount);
                break;

            case Windows.System.VirtualKey.Space:
                if (!_isMiddleButtonAutoScrolling)
                {
                    if (_isAutoScrolling)
                    {
                        StopAutoScrolling();
                    }
                    else
                    {
                        StartAutoScrolling();
                    }
                }
                break;

            case Windows.System.VirtualKey.R:
                {
                    int page = Random.Shared.Next(Math.Max(1, PageCount)) + 1;
                    SetScrollViewer2("JumpToRandomPageUsingRKey", ScrollSource.User, page: page);
                }
                break;

            default:
                handled = false;
                break;
        }

        if (handled)
        {
            e.Handled = true;
        }
    }

    #endregion

    #region Auto scrolling

    private int _autoScrollSpeed = 0;
    private bool _isAutoScrolling = false;
    private bool _stopAutoScrollingRequested = false;
    private double _autoScrollParallelVelocity = 0.0;
    private double _autoScrollPerpendicularVelocity = 0.0;
    private bool _isMiddleButtonAutoScrolling = false;
    private Windows.Foundation.Point _middleButtonAutoScrollOrigin;

    private bool IsAutoScrollEnabled => _autoScrollSpeed > 0;

    private void StartMiddleButtonAutoScrolling(Windows.Foundation.Point point)
    {
        if (_isMiddleButtonAutoScrolling || !IsAutoScrollEnabled)
        {
            return;
        }

        _isMiddleButtonAutoScrolling = true;
        _middleButtonAutoScrollOrigin = point;
        MiddleButtonMarkerRectangle.Visibility = Visibility.Visible;
        MiddleButtonMarkerRectangle.Margin = new Thickness(point.X, point.Y, 0, 0);
        StartAutoScrollingInternal(0, 0);
    }

    private void UpdateMiddleButtonAutoScrolling(Windows.Foundation.Point point)
    {
        if (!_isMiddleButtonAutoScrolling)
        {
            return;
        }

        double velocityX = point.X - _middleButtonAutoScrollOrigin.X;
        double velocityY = point.Y - _middleButtonAutoScrollOrigin.Y;
        double parallelVelocity = _isVertical ? velocityY : (_isLeftToRight ? velocityX : -velocityX);
        double perpendicularVelocity = _isVertical ? velocityX : velocityY;
        parallelVelocity *= (double)_autoScrollSpeed / AUTO_SCROLL_COMMON_SPEED;
        if (_isContinuous)
        {
            parallelVelocity *= AUTO_SCROLL_PANNING_VELOCITY_MULTIPLIER_CONTINUOUS;
            perpendicularVelocity *= AUTO_SCROLL_PANNING_VELOCITY_MULTIPLIER_CONTINUOUS;
        }
        else
        {
            parallelVelocity *= AUTO_SCROLL_PANNING_VELOCITY_MULTIPLIER_SEPERATE;
            perpendicularVelocity *= AUTO_SCROLL_PANNING_VELOCITY_MULTIPLIER_SEPERATE;
        }

        StartAutoScrollingInternal(parallelVelocity, perpendicularVelocity);
    }

    private void StopMiddleButtonAutoScrolling()
    {
        if (!_isMiddleButtonAutoScrolling)
        {
            return;
        }

        _isMiddleButtonAutoScrolling = false;
        MiddleButtonMarkerRectangle.Visibility = Visibility.Collapsed;
        StopAutoScrolling();
    }

    private void StartAutoScrolling(double? velocity = null)
    {
        if (!IsAutoScrollEnabled)
        {
            return;
        }

        double velocityValue;
        if (_isContinuous)
        {
            velocity ??= _internalDB?.AutoScrollVelocity;
            if (velocity.HasValue)
            {
                velocityValue = velocity.Value;
            }
            else
            {
                velocityValue = AUTO_SCROLL_COMMON_DEFAULT_VELOCITY * _autoScrollSpeed / AUTO_SCROLL_COMMON_SPEED;
            }

            _internalDB?.AutoScrollVelocity = velocityValue;
        }
        else
        {
            velocityValue = 1000.0 * _autoScrollSpeed / ((double)AUTO_SCROLL_COMMON_INTERVAL * AUTO_SCROLL_COMMON_SPEED);
        }

        StartAutoScrollingInternal(velocityValue, 0);
    }

    private void StopAutoScrolling()
    {
        _stopAutoScrollingRequested = true;
    }

    private void StartAutoScrollingInternal(double parallelVelocity, double perpendicularVelocity)
    {
        _stopAutoScrollingRequested = false;
        _autoScrollParallelVelocity = parallelVelocity;
        _autoScrollPerpendicularVelocity = perpendicularVelocity;
        if (_isAutoScrolling)
        {
            UpdateReaderStatusText();
            return;
        }

        _isAutoScrolling = true;
        Log("AutoScroll", $"StartVelocity=({parallelVelocity},{perpendicularVelocity})");
        UpdateReaderStatusText();
        ReaderEventAutoScrollingChanged?.Invoke(this, true);

        DispatcherQueueTimer timer = MainThreadUtils.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(16);

        long lastTick = 0;
        bool isContinuous = _isContinuous;
        timer.Tick += (_, _) =>
        {
            if (!_isAutoScrolling)
            {
                return;
            }

            if (_stopAutoScrollingRequested || !IsAutoScrollEnabled || !_isLoaded || isContinuous != _isContinuous)
            {
                timer.Stop();
                _isAutoScrolling = false;
                StopMiddleButtonAutoScrolling();
                Log("AutoScroll", "Stop");
                UpdateReaderStatusText();
                ReaderEventAutoScrollingChanged?.Invoke(this, false);
                return;
            }

            long currentTime = GetTicks();
            if (lastTick == 0)
            {
                lastTick = currentTime;
                return;
            }

            int elapsed = (int)(currentTime - lastTick);
            if (_isContinuous)
            {
                double parallelDelta = _autoScrollParallelVelocity * elapsed;
                double perpendicularDelta = _autoScrollPerpendicularVelocity * elapsed;
                lastTick = currentTime;
                SetScrollViewer1("AutoScroll", ScrollSource.AutoScroll,
                    parallelOffset: SCParallelOffsetFinal + parallelDelta,
                    perpendicularOffset: SCPerpendicularOffsetFinal + perpendicularDelta);
            }
            else
            {
                double targetDelay = 1000.0 / Math.Abs(_autoScrollParallelVelocity); // (0, PositiveInfinite)

                int frameIndex = SCCurrentFrameIndexFinal;
                if (frameIndex >= 0 && frameIndex < _frameItemsSource.Count)
                {
                    ReaderFrameViewModel frame = _frameItemsSource[frameIndex];
                    if (frame.PageL != ReaderFrameViewModel.NO_PAGE && frame.PageR != ReaderFrameViewModel.NO_PAGE)
                    {
                        targetDelay *= AUTO_SCROLL_DUAL_FRAME_MULTIPLIER;
                    }
                }

                if (elapsed > targetDelay)
                {
                    lastTick = currentTime;
                    if (double.IsPositive(_autoScrollParallelVelocity))
                    {
                        MoveFrameInternal("AutoScrolling", ScrollSource.AutoScroll, 1);
                    }
                    else
                    {
                        MoveFrameInternal("AutoScrolling", ScrollSource.AutoScroll, -1);
                    }
                }
            }
        };
        timer.Start();
    }

    #endregion

    #region Over Scroll

    private bool _overScrollStarted = false;
    private double _overScrollAmount = 0.0;

    private void UpdateOverScrollAmount(double increment)
    {
        if (!OverScrollEnabled)
        {
            if (_overScrollStarted)
            {
                _overScrollStarted = false;
                UpdateOverScrollStatus();
            }

            return;
        }

        if (!_overScrollStarted)
        {
            if (Math.Abs(increment) > 1E-2)
            {
                _overScrollAmount = 0.0;
                _overScrollStarted = true;
            }

            return;
        }

        _overScrollAmount += increment;
        UpdateOverScrollStatus();
    }

    private void ResetOverScrollAmount()
    {
        if (!_overScrollStarted)
        {
            return;
        }

        _overScrollStarted = false;
        _overScrollAmount = 0.0;
        UpdateOverScrollStatus();
    }

    private void UpdateOverScrollStatus()
    {
        double maxOverScrollAmount = ViewportParallelLength * 0.4;

        if (!_overScrollStarted)
        {
            OverScrollProgressRing.Visibility = Visibility.Collapsed;
            return;
        }

        double ratio = Math.Abs(_overScrollAmount) / maxOverScrollAmount;
        if (ratio > 1.0)
        {
            bool forward = double.IsPositive(_overScrollAmount);
            _overScrollStarted = false;
            OverScrollProgressRing.Visibility = Visibility.Collapsed;
            DispatchOverScrollEvent(forward);
            return;
        }

        OverScrollProgressRing.Value = ratio * 100.0;
        OverScrollProgressRing.Visibility = Visibility.Visible;
    }

    private void DispatchOverScrollEvent(bool forward)
    {
        if (!OverScrollEnabled)
        {
            return;
        }

        ReaderEventOverScroll?.Invoke(this, forward);
    }

    #endregion

    #region Cursor

    private bool _cursorDisposed = true;
    private long _hideCursorTime = -1;
    private bool _postHideCursor = false;

    private void HideCursorDelayed(int delayMilliseconds)
    {
        if (delayMilliseconds <= 0)
        {
            HideCursor();
            return;
        }

        long targetTime = GetTicks() + delayMilliseconds;
        if (_postHideCursor && targetTime >= _hideCursorTime)
        {
            _hideCursorTime = targetTime;
            return;
        }

        _hideCursorTime = targetTime;

        void HideCursorIfNeeded()
        {
            if (_hideCursorTime == -1)
            {
                return;
            }

            long currentTime = GetTicks();
            if (currentTime >= _hideCursorTime)
            {
                HideCursor();
            }
            else
            {
                _postHideCursor = true;
                PostToCurrentThread(delegate
                {
                    _postHideCursor = false;
                    HideCursorIfNeeded();
                }, (int)(_hideCursorTime - currentTime));
            }
        }

        _postHideCursor = true;
        PostToCurrentThread(delegate
        {
            _postHideCursor = false;
            HideCursorIfNeeded();
        }, delayMilliseconds);
    }

    private void ShowCursor()
    {
        DisposeCursor();
    }

    private void HideCursor()
    {
        _hideCursorTime = -1;

        InputCursor? cursor = ProtectedCursor;
        if (_cursorDisposed && cursor is not null)
        {
            return;
        }

        _cursorDisposed = true;
        cursor ??= InputSystemCursor.Create(InputSystemCursorShape.Arrow);
        cursor.Dispose();
        ProtectedCursor = cursor;
    }

    private void DisposeCursor()
    {
        _hideCursorTime = -1;

        InputCursor? cursor = ProtectedCursor;
        if (_cursorDisposed && cursor is null)
        {
            return;
        }

        _cursorDisposed = true;
        cursor?.Dispose();
        ProtectedCursor = null;
    }

    #endregion

    #region Scroll Controller

    private bool _isCommitting = false;
    private float _zoom = 1F;
    private bool _finalValueSynced = false;
    private bool _isPreciseScrolling = false;

    private ScrollViewer ThisScrollViewer => ContentScrollViewer;
    private ReaderListView ThisListView => ContentListView;
    private float ZoomFactor => ThisScrollViewer.ZoomFactor;
    private double HorizontalOffset => ThisScrollViewer.HorizontalOffset;
    private double VerticalOffset => ThisScrollViewer.VerticalOffset;
    private double ParallelOffset => _isVertical ? VerticalOffset : HorizontalOffset;
    private double ViewportWidth => ThisScrollViewer.ViewportWidth;
    private double ViewportHeight => ThisScrollViewer.ViewportHeight;
    private double ViewportParallelLength => _isVertical ? ViewportHeight : ViewportWidth;
    private double ViewportPerpendicularLength => _isVertical ? ViewportWidth : ViewportHeight;
    private double ContentPerpendicularLength => _isVertical ? ThisListView.ActualWidth : ThisListView.ActualHeight;

    private int _SCCurrentFrameIndexFinal;
    private int SCCurrentFrameIndexFinal
    {
        get
        {
            SCSyncFinalVal();
            return _SCCurrentFrameIndexFinal;
        }
        set
        {
            SCSyncFinalVal();
            _SCCurrentFrameIndexFinal = value;
        }
    }

    private float _SCZoomFactorFinal;
    private float SCZoomFactorFinal
    {
        get
        {
            SCSyncFinalVal();
            return _SCZoomFactorFinal;
        }
        set
        {
            SCSyncFinalVal();
            _SCZoomFactorFinal = value;
        }
    }

    private double _SCHorizontalOffsetFinal;
    private double SCHorizontalOffsetFinal
    {
        get
        {
            SCSyncFinalVal();
            return _SCHorizontalOffsetFinal;
        }
        set
        {
            SCSyncFinalVal();
            _SCHorizontalOffsetFinal = value;
        }
    }

    private double _SCVerticalOffsetFinal;
    private double SCVerticalOffsetFinal
    {
        get
        {
            SCSyncFinalVal();
            return _SCVerticalOffsetFinal;
        }
        set
        {
            SCSyncFinalVal();
            _SCVerticalOffsetFinal = value;
        }
    }

    private bool _SCDisableAnimationFinal;
    private bool SCDisableAnimationFinal
    {
        get
        {
            SCSyncFinalVal();
            return _SCDisableAnimationFinal;
        }
        set
        {
            SCSyncFinalVal();
            _SCDisableAnimationFinal = value;
        }
    }

    private double SCParallelOffsetFinal => _isVertical ? SCVerticalOffsetFinal : SCHorizontalOffsetFinal;
    private double SCPerpendicularOffsetFinal => _isVertical ? SCHorizontalOffsetFinal : SCVerticalOffsetFinal;

    private void SCSyncFinalVal()
    {
        if (!_isLoaded || _finalValueSynced)
        {
            return;
        }

        _finalValueSynced = true;
        _SCCurrentFrameIndexFinal = CurrentFrameIndex;
        _SCHorizontalOffsetFinal = HorizontalOffset;
        _SCVerticalOffsetFinal = VerticalOffset;
        _SCZoomFactorFinal = ZoomFactor;
        _SCDisableAnimationFinal = false;
    }

    private void SCClearFinalVal(string reason)
    {
        Log("ClearFinalValue", reason);
        _finalValueSynced = false;
    }

    private void MoveFrameByUser(string reason, int increment)
    {
        MoveFrameInternal(reason, ScrollSource.User, increment);
    }

    private void MoveFrameInternal(string reason, ScrollSource source, int increment)
    {
        int currentFrame = SCCurrentFrameIndexFinal;
        if (currentFrame < 0 || currentFrame >= _frameItemsSource.Count)
        {
            return;
        }

        double currentPage = CurrentPage;
        double currentFramePage = _frameItemsSource[currentFrame].Page;
        double pageDiff = currentPage - currentFramePage;

        if (pageDiff > 0.1 && increment < 0)
        {
            increment++;
        }
        else if (pageDiff < -0.1 && increment > 0)
        {
            increment--;
        }

        int targetFrame = currentFrame + increment;

        if (targetFrame >= _frameItemsSource.Count)
        {
            DispatchOverScrollEvent(true);
            return;
        }

        if (targetFrame < 0)
        {
            DispatchOverScrollEvent(false);
            return;
        }

        double targetPage = _frameItemsSource[targetFrame].Page;
        float? zoom = _zoom > 1.01F ? 1F : null;
        SetScrollViewer2(reason, source, zoom: zoom, page: targetPage, disableAnimation: !AppSettingsModel.Instance.TransitionAnimation);
    }

    private ScrollResult SetScrollViewer1(
        string reason,
        ScrollSource source,
        float? zoom = null,
        double? parallelOffset = null,
        double? perpendicularOffset = null,
        bool disableAnimation = true)
    {
        double? horizontalOffset = _isVertical ? perpendicularOffset : parallelOffset;
        double? verticalOffset = _isVertical ? parallelOffset : perpendicularOffset;

        return SetScrollViewerInternal(new ScrollRequest(source)
        {
            Zoom = zoom,
            HorizontalOffset = horizontalOffset,
            VerticalOffset = verticalOffset,
            DisableAnimation = disableAnimation,
            IgnoreTooClose = _isViewChanging,
        }, reason);
    }

    private ScrollResult SetScrollViewer2(
        string reason,
        ScrollSource source,
        float? zoom = null,
        ZoomType zoomType = ZoomType.CenterInside,
        double? page = null,
        bool applyParallelOffset = true,
        bool disableAnimation = true)
    {
        double? horizontalOffset = null;
        double? verticalOffset = null;
        int? frameIndex = null;

        if (page.HasValue)
        {
            double targetPage = page.Value;

            Tuple<double, double>? offsets = PageOffset(targetPage);
            if (offsets is null)
            {
                Log("Jump", "NullOffset", $"P={targetPage}");
                return ScrollResult.UnknownFailure;
            }

            double parallelOffset = offsets.Item1;
            double perpendicularOffset = offsets.Item2;
            ConvertOffset(ref horizontalOffset, ref verticalOffset, applyParallelOffset ? parallelOffset : null, perpendicularOffset);

            if (applyParallelOffset)
            {
                if (!TryConvertPageToFrameIndex(ToDiscretePage(targetPage), out int targetFrameIndex))
                {
                    Log("Jump", "FrameConversionFailed", $"P={targetPage}");
                    return ScrollResult.UnknownFailure;
                }

                frameIndex = targetFrameIndex;
            }
        }

        return SetScrollViewerInternal(new ScrollRequest(source)
        {
            Zoom = zoom,
            ZoomType = zoomType,
            FrameIndex = frameIndex,
            HorizontalOffset = horizontalOffset,
            VerticalOffset = verticalOffset,
            DisableAnimation = disableAnimation,
            IgnoreTooClose = _isViewChanging,
        }, reason);
    }

    private ScrollResult SetScrollViewer3(
        string reason,
        ScrollSource source,
        float? zoom = null,
        ZoomType zoomType = ZoomType.CenterInside,
        double? horizontalOffset = null,
        double? verticalOffset = null,
        bool disableAnimation = true)
    {
        return SetScrollViewerInternal(new ScrollRequest(source)
        {
            Zoom = zoom,
            ZoomType = zoomType,
            HorizontalOffset = horizontalOffset,
            VerticalOffset = verticalOffset,
            DisableAnimation = disableAnimation,
            IgnoreTooClose = _isViewChanging,
        }, reason);
    }

    private ScrollResult SetScrollViewerInternal(ScrollRequest request, string reason)
    {
        Log("Jump", "Request",
            $"Reason={reason}",
            $"Src={(int)request.Source}",
            $"F={request.FrameIndex}",
            $"Z={request.Zoom}",
            $"H={request.HorizontalOffset}",
            $"V={request.VerticalOffset}",
            $"D={request.DisableAnimation}");

        var context = new ScrollContext
        {
            Zoom = request.Zoom,
            DisableAnimation = request.DisableAnimation,
            HorizontalOffset = request.HorizontalOffset,
            VerticalOffset = request.VerticalOffset,
        };

        SetScrollViewerInternal(request, context);

        switch (context.Result)
        {
            case ScrollResult.None:
                Logger.F(TAG, "Scroll result not set");
                break;
            case ScrollResult.Success:
                if (request.Source != ScrollSource.Programmatic)
                {
                    if (Math.Abs(context.ScrollAmount) > 1E-2 || _isInInertiaTranslation)
                    {
                        ResetOverScrollAmount();
                    }
                    else
                    {
                        // Positive over scroll can only occur when last frame is loaded
                        if (double.IsNegative(context.OverScrollAmount) || _isLastFrameLoaded)
                        {
                            UpdateOverScrollAmount(context.OverScrollAmount);
                        }
                    }
                }
                break;
            default:
                break;
        }

        return context.Result;
    }

    private void SetScrollViewerInternal(ScrollRequest request, ScrollContext context)
    {
        if (!_isInitialFrameActionPerformed)
        {
            Log("Jump", "Failed", "Reason=NotLoaded");
            context.Result = ScrollResult.UnknownFailure;
            return;
        }

        if (_isCommitting)
        {
            Log("Jump", "Failed", "Reason=IsCommitting");
            context.Result = ScrollResult.UnknownFailure;
            return;
        }

        Logger.Assert(float.IsFinite(request.Zoom ?? 0), "5D42C4251571A722");
        Logger.Assert(!float.IsNegative(request.Zoom ?? 0), "65075662668EE56D");
        Logger.Assert(double.IsFinite(request.HorizontalOffset ?? 0), "4FD89F79946B8D03");
        Logger.Assert(double.IsFinite(request.VerticalOffset ?? 0), "6678A0ED7D2FEB43");

        if (request.Source == ScrollSource.User || request.Source == ScrollSource.UserPrecise)
        {
            _isPreciseScrolling = request.Source == ScrollSource.UserPrecise;

            // User interaction cancels auto scrolling
            StopAutoScrolling();
        }

        SetScrollViewerZoom(request, context);
        if (context.Result != ScrollResult.None)
        {
            return;
        }

        Logger.Assert(float.IsFinite(context.Zoom ?? 0), "8E76EB6D567DCCB9");
        Logger.Assert(!float.IsNegative(context.Zoom ?? 0), "7D83986CC7231EAE");
        Logger.Assert(float.IsFinite(context.ZoomFactor ?? 0), "92391D195B22B685");
        Logger.Assert(!float.IsNegative(context.ZoomFactor ?? 0), "358402C4AFBEA082");
        Logger.Assert(double.IsFinite(context.HorizontalOffset ?? 0), "473A38A62A78DD26");
        Logger.Assert(double.IsFinite(context.VerticalOffset ?? 0), "4C7278673747BA28");

        if (context.HorizontalOffset.HasValue)
        {
            context.HorizontalOffset = Math.Max(0, context.HorizontalOffset.Value);
        }

        if (context.VerticalOffset.HasValue)
        {
            context.VerticalOffset = Math.Max(0, context.VerticalOffset.Value);
        }

        Log("Jump", "ParamAfterZoom",
            $"Z={context.Zoom}",
            $"ZF={context.ZoomFactor}",
            $"H={context.HorizontalOffset}",
            $"V={context.VerticalOffset}",
            $"D={context.DisableAnimation}");

        AdjustParallelOffset(context);

        Logger.Assert(float.IsFinite(context.Zoom ?? 0), "6BC2B5793E12AFA4");
        Logger.Assert(!float.IsNegative(context.Zoom ?? 0), "CF5A68638CB59852");
        Logger.Assert(float.IsFinite(context.ZoomFactor ?? 0), "FF0AD921D9E8BBB0");
        Logger.Assert(!float.IsNegative(context.ZoomFactor ?? 0), "C226B0EBAC496CED");
        Logger.Assert(double.IsFinite(context.HorizontalOffset ?? 0), "A1FF6DDBAD093F79");
        Logger.Assert(double.IsFinite(context.VerticalOffset ?? 0), "C8D35D8BDDF468F8");

        Log("Jump", "ParamAfterFix",
            $"Z={context.Zoom}",
            $"ZF={context.ZoomFactor}",
            $"H={context.HorizontalOffset}",
            $"V={context.VerticalOffset}",
            $"D={context.DisableAnimation}");

        if (request.FrameIndex.HasValue)
        {
            SCCurrentFrameIndexFinal = request.FrameIndex.Value;
        }

        if (context.Zoom.HasValue)
        {
            _zoom = context.Zoom.Value;
        }

        if (context.HorizontalOffset == null && context.VerticalOffset == null && context.ZoomFactor == null)
        {
            context.Result = ScrollResult.Success;
            return;
        }

        if (request.IgnoreTooClose)
        {
            double verticalOffsetDiff = context.VerticalOffset.HasValue ? SCVerticalOffsetFinal - context.VerticalOffset.Value : 0.0;
            double horizontalOffsetDiff = context.HorizontalOffset.HasValue ? SCHorizontalOffsetFinal - context.HorizontalOffset.Value : 0.0;
            double zoomFactorDiff = context.ZoomFactor.HasValue ? context.ZoomFactor.Value / Math.Max(SCZoomFactorFinal, 1E-5) : 1.0;
            if (Math.Abs(verticalOffsetDiff) <= 5.0 && Math.Abs(horizontalOffsetDiff) <= 5.0 && Math.Abs(zoomFactorDiff - 1.0) <= 0.01)
            {
                // Ignore the request if target offset is really close to the current offset,
                // otherwise we might trigger a dead loop
                Log("Jump", "Cancelled", "Reason=TooClose");
                context.Result = ScrollResult.TooClose;
                return;
            }
        }

        context.Result = ChangeView(request, context);
    }

    private void SetScrollViewerZoom(ScrollRequest request, ScrollContext context)
    {
        // Calculate zoom coefficient for new frame
        int newFrameIndex = request.FrameIndex ?? SCCurrentFrameIndexFinal;
        if (newFrameIndex < 0 || newFrameIndex >= _frameItemsSource.Count)
        {
            context.Result = ScrollResult.UnknownFailure;
            return;
        }

        ReaderFrameViewModel newFrame = _frameItemsSource[newFrameIndex];
        ZoomCoefficient? zoomCoefficientNew = CalculateZoomCoefficient(newFrame);
        Log("Jump", "Zoom#1",
            $"FN={newFrameIndex}",
            $"ZCN={zoomCoefficientNew}");

        if (zoomCoefficientNew == null)
        {
            context.Result = ScrollResult.UnknownFailure;
            return;
        }

        // Calculate zoom factor
        double centerCropMultipier = zoomCoefficientNew.Max() / zoomCoefficientNew.Min();
        double zoom;
        if (request.Zoom.HasValue)
        {
            zoom = request.Zoom.Value;
            switch (request.ZoomType)
            {
                case ZoomType.CenterInside:
                    break;
                case ZoomType.CenterCrop:
                    zoom *= centerCropMultipier;
                    break;
                case ZoomType.FitWidthDualAware:
                    zoom *= zoomCoefficientNew.FitWidth / zoomCoefficientNew.Min();
                    if (newFrame.IsDualPage)
                    {
                        zoom *= DUAL_FRAME_DEFAULT_WIDTH_MULTIPLIER;
                    }

                    break;
                case ZoomType.FitHeight:
                    zoom *= zoomCoefficientNew.FitHeight / zoomCoefficientNew.Min();
                    break;
                default:
                    Logger.F(TAG, "Unknown zoom type");
                    goto case ZoomType.CenterInside;
            }
        }
        else
        {
            int frame = SCCurrentFrameIndexFinal;
            if (frame < 0 || frame >= _frameItemsSource.Count)
            {
                frame = 0;
            }

            ZoomCoefficient zoomCoefficient = zoomCoefficientNew;
            if (frame != newFrameIndex)
            {
                ZoomCoefficient? zoomCoefficientTest = CalculateZoomCoefficient(frame);
                if (zoomCoefficientTest != null)
                {
                    zoomCoefficient = zoomCoefficientTest;
                }
            }

            zoom = (double)SCZoomFactorFinal / zoomCoefficient.Min();
        }

        double zoomFactorNew = zoom * zoomCoefficientNew.Min();
        double maxZoomFactor = Math.Max(_maxZoomFactor, MAX_ZOOM * zoomCoefficientNew.Max());
        double minZoomFactor = Math.Min(_minZoomFactor, Math.Min(MIN_ZOOM_CENTER_INSIDE * zoomCoefficientNew.Min(), MIN_ZOOM_CENTER_CROP * zoomCoefficientNew.Max()));
        zoomFactorNew = Math.Min(zoomFactorNew, maxZoomFactor);
        zoomFactorNew = Math.Max(zoomFactorNew, minZoomFactor);
        zoom = zoomFactorNew / zoomCoefficientNew.Min();
        context.ZoomFactor = (float)zoomFactorNew;
        context.Zoom = (float)zoom;

        // Apply zooming
        double zoomFactorBefore = SCZoomFactorFinal;
        double zoomFactorAfter = (float)context.ZoomFactor;
        double zoomChangeRatio = zoomFactorAfter / zoomFactorBefore;
        double extraPaddingBefore = CalculateExtraPerpendicularPadding(zoomFactorBefore);
        double extraPaddingAfter = CalculateExtraPerpendicularPadding(zoomFactorAfter);
        double halfViewportWidth = ViewportWidth * 0.5;
        double halfViewportHeight = ViewportHeight * 0.5;
        context.HorizontalOffset ??= SCHorizontalOffsetFinal;
        context.VerticalOffset ??= SCVerticalOffsetFinal;

        Log("Jump", "Zoom#2",
            $"ZF1={zoomFactorBefore}",
            $"ZF2={zoomFactorAfter}",
            $"Ratio={zoomChangeRatio}",
            $"Pd1={extraPaddingBefore}",
            $"Pd2={extraPaddingAfter}",
            $"VW={halfViewportWidth}",
            $"VH={halfViewportHeight}",
            $"HO={context.HorizontalOffset}",
            $"VO={context.VerticalOffset}");

        if (_isVertical)
        {
            context.HorizontalOffset += halfViewportWidth - extraPaddingBefore;
            context.HorizontalOffset *= zoomChangeRatio;
            context.HorizontalOffset -= halfViewportWidth - extraPaddingAfter;
            context.VerticalOffset += halfViewportHeight;
            context.VerticalOffset *= zoomChangeRatio;
            context.VerticalOffset -= halfViewportHeight;
        }
        else
        {
            context.HorizontalOffset += halfViewportWidth;
            context.HorizontalOffset *= zoomChangeRatio;
            context.HorizontalOffset -= halfViewportWidth;
            context.VerticalOffset += halfViewportHeight - extraPaddingBefore;
            context.VerticalOffset *= zoomChangeRatio;
            context.VerticalOffset -= halfViewportHeight - extraPaddingAfter;
        }

        context.HorizontalOffset = Math.Max(0.0, context.HorizontalOffset.Value);
        context.VerticalOffset = Math.Max(0.0, context.VerticalOffset.Value);
    }

    private ScrollResult ChangeView(ScrollRequest request, ScrollContext context)
    {
        if (_isVertical)
        {
            if (context.VerticalOffset.HasValue)
            {
                context.ScrollAmount = context.VerticalOffset.Value - SCVerticalOffsetFinal;
            }
        }
        else
        {
            if (context.HorizontalOffset.HasValue)
            {
                context.ScrollAmount = context.HorizontalOffset.Value - SCHorizontalOffsetFinal;
            }
        }

        if (context.HorizontalOffset.HasValue)
        {
            SCHorizontalOffsetFinal = context.HorizontalOffset.Value;
        }

        if (context.VerticalOffset.HasValue)
        {
            SCVerticalOffsetFinal = context.VerticalOffset.Value;
        }

        if (context.ZoomFactor.HasValue)
        {
            SCZoomFactorFinal = context.ZoomFactor.Value;
        }

        if (context.DisableAnimation)
        {
            SCDisableAnimationFinal = true;
        }

        double commitHorizontalOffset = SCHorizontalOffsetFinal;
        double commitVerticalOffset = SCVerticalOffsetFinal;
        float commitZoomFactor = SCZoomFactorFinal;
        bool commitDisableAnimation = SCDisableAnimationFinal;

        float oldZoomFactor = ZoomFactor;
        double oldHorizontalOffset = HorizontalOffset;
        double oldVerticalOffset = VerticalOffset;

        bool changed;
        _isCommitting = true;
        try
        {
            changed = ThisScrollViewer.ChangeView(
                commitHorizontalOffset,
                commitVerticalOffset,
                commitZoomFactor,
                commitDisableAnimation);
        }
        finally
        {
            _isCommitting = false;
        }

        Log("Jump", "Commit",
            $"Changed={changed}",
            $"Z={commitZoomFactor}",
            $"H={commitHorizontalOffset}",
            $"V={commitVerticalOffset}",
            $"D={commitDisableAnimation}",
            $"LW={ThisListView.ActualWidth}",
            $"LH={ThisListView.ActualHeight}");

        if (!changed)
        {
            if (oldZoomFactor == commitZoomFactor &&
                oldHorizontalOffset == commitHorizontalOffset &&
                oldVerticalOffset == commitVerticalOffset)
            {
                return ScrollResult.Unchanged;
            }

            return ScrollResult.UnknownFailure;
        }

        return ScrollResult.Success;
    }

    private void AdjustParallelOffset(ScrollContext context)
    {
        if (_frameItemsSource.Count == 0)
        {
            return;
        }

        double zoom = context.ZoomFactor ?? SCZoomFactorFinal;
        double parallelOffset;
        if (_isVertical)
        {
            if (!context.VerticalOffset.HasValue)
            {
                return;
            }

            parallelOffset = context.VerticalOffset.Value;
        }
        else
        {
            if (!context.HorizontalOffset.HasValue)
            {
                return;
            }

            parallelOffset = context.HorizontalOffset.Value;
        }

        double screenCenterOffset = ViewportParallelLength * 0.5 + parallelOffset;

        double? movementForward = null;
        if (_isFirstFrameLoaded)
        {
            Thickness firstFrameMargin = _frameItemsSource[0].FrameMargin;
            double frameMarginStart = _isVertical ? firstFrameMargin.Top : firstFrameMargin.Left;
            double imageStartOffset = frameMarginStart * zoom;
            movementForward = imageStartOffset - screenCenterOffset;
        }

        double? movementBackward = null;
        if (_isLastFrameLoaded)
        {
            // ExtentLength is unreliable, use frame offset instead
            FrameOffsetData? lastFrameOffset = FrameOffset(_frameItemsSource.Count - 1);
            if (lastFrameOffset.HasValue)
            {
                double imageEndOffset = lastFrameOffset.Value.ParallelEnd * zoom;
                movementBackward = screenCenterOffset - imageEndOffset;
            }
        }

        double movement = 0.0;
        bool canMove = false;

        if (movementForward.HasValue && movementForward.Value > 0)
        {
            canMove = true;
            movement += movementForward.Value;
        }

        if (movementBackward.HasValue && movementBackward.Value > 0)
        {
            canMove = true;
            movement -= movementBackward.Value;
        }

        if (!canMove)
        {
            return;
        }

        context.OverScrollAmount = -movement;
        if (_isVertical)
        {
            context.VerticalOffset += movement;
        }
        else
        {
            context.HorizontalOffset += movement;
        }
    }

    private ZoomCoefficient? CalculateZoomCoefficient(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= _frameItemsSource.Count)
        {
            return null;
        }

        return CalculateZoomCoefficient(_frameItemsSource[frameIndex]);
    }

    private ZoomCoefficient? CalculateZoomCoefficient(ReaderFrameViewModel frame)
    {
        double viewportWidth = ViewportWidth;
        double viewportHeight = ViewportHeight;
        double frameWidth = frame.FrameWidth;
        double frameHeight = frame.FrameHeight;

        double minValue = Math.Min(viewportWidth, viewportHeight);
        minValue = Math.Min(minValue, frameWidth);
        minValue = Math.Min(minValue, frameHeight);
        if (minValue < 0.1)
        {
            return null;
        }

        return new ZoomCoefficient
        {
            FitWidth = viewportWidth / frameWidth,
            FitHeight = viewportHeight / frameHeight
        };
    }

    private double CalculateExtraPerpendicularPadding(double zoom)
    {
        double padding = (ViewportPerpendicularLength - ContentPerpendicularLength * zoom) * 0.5;
        return Math.Max(padding, 0);
    }

    #endregion

    #region Offset Calculator

    private Tuple<double, double>? PageOffset(double page)
    {
        Logger.Assert(double.IsFinite(page), "251D69B9AD4BFDDA");

        if (PageCount <= 0)
        {
            return null;
        }

        // Valid range is [0.5, PageCount + 0.5]
        page = Math.Min(page, PageCount + 0.5);
        page = Math.Max(page, 0.5);

        int nearestPage = Math.Clamp((int)Math.Round(page, MidpointRounding.AwayFromZero), 1, PageCount);
        if (!TryConvertPageToFrameIndex(nearestPage, out int nearestFrame))
        {
            return null;
        }

        AnchorConverter? converter = CreateAnchorConverter(nearestFrame, nearestFrame + 1);
        if (converter is null)
        {
            return null;
        }

        double parallelOffset = converter.CalculateOffset(page);
        parallelOffset = parallelOffset * SCZoomFactorFinal - ViewportParallelLength * 0.5;

        FrameOffsetData? offsets = FrameOffset(nearestFrame);
        if (!offsets.HasValue)
        {
            return null;
        }

        double perpendicularOffset = offsets.Value.PerpendicularCenter * SCZoomFactorFinal -
            ViewportPerpendicularLength * 0.5;

        // Negative offset indicates that the scrollable content is smaller than the
        // visible area of the ScrollViewer, in that case offset should be clamped to
        // zero.
        perpendicularOffset = Math.Max(perpendicularOffset, 0.0);

        var result = new Tuple<double, double>(parallelOffset, perpendicularOffset);

        Logger.Assert(double.IsFinite(result.Item1), "F00BE2F8D9D28D43");
        Logger.Assert(double.IsFinite(result.Item2), "FAD6B4BA580151CF");

        return result;
    }

    private AnchorConverter? CreateAnchorConverter(int startFrame, int endFrame)
    {
        if (_frameItemsSource.Count == 0 || startFrame >= endFrame || startFrame < 0 || endFrame > _frameItemsSource.Count)
        {
            return null;
        }

        var anchorConverter = new AnchorConverter();

        int minFrame = Math.Max(startFrame - 1, 0);
        int maxFrame = Math.Min(endFrame + 1, _frameItemsSource.Count);
        FrameOffsetData? lastOffset = null;
        for (int frame = minFrame; frame < maxFrame; frame++)
        {
            FrameOffsetData? offset = FrameOffset(frame);
            if (!offset.HasValue)
            {
                break;
            }

            ReaderFrameViewModel frameModel = _frameItemsSource[frame];
            if (frameModel.IsEmpty)
            {
                break;
            }

            if (frame == 0)
            {
                anchorConverter.Insert(0.5, offset.Value.ParallelStart);
            }

            if (frame == _frameItemsSource.Count - 1)
            {
                anchorConverter.Insert(PageCount + 0.5, offset.Value.ParallelEnd);
            }

            if (_isVertical)
            {
                double page = frameModel.Page;
                double frameCenterOffset = (offset.Value.ParallelStart + offset.Value.ParallelEnd) * 0.5;
                anchorConverter.Insert(page, frameCenterOffset);
            }
            else
            {
                if (frameModel.IsDualPage)
                {
                    double startImageWidth = _isLeftToRight ? frameModel.LeftImageWidth : frameModel.RightImageWidth;
                    double endImageWidth = _isLeftToRight ? frameModel.RightImageWidth : frameModel.LeftImageWidth;

                    int minPage = frameModel.MinPage;
                    double minPageCenterOffset = offset.Value.ParallelStart + startImageWidth * 0.5;
                    anchorConverter.Insert(minPage, minPageCenterOffset);

                    int maxPage = frameModel.MaxPage;
                    double maxPageCenterOffset = offset.Value.ParallelEnd - endImageWidth * 0.5;
                    anchorConverter.Insert(maxPage, maxPageCenterOffset);

                    double midPointOffset = offset.Value.ParallelStart + startImageWidth;
                    double midPointPage = (minPage + maxPage) * 0.5;
                    anchorConverter.Insert(midPointPage, midPointOffset);
                }
                else
                {
                    int page = frameModel.MinPage;
                    double pageCenterOffset = offset.Value.ParallelStart + frameModel.FrameWidth * 0.5;
                    anchorConverter.Insert(page, pageCenterOffset);
                }
            }

            if (lastOffset.HasValue)
            {
                double midPointOffset = (offset.Value.ParallelStart + lastOffset.Value.ParallelEnd) * 0.5;
                double midPointPage = (_frameItemsSource[frame - 1].MaxPage + frameModel.MinPage) * 0.5;
                anchorConverter.Insert(midPointPage, midPointOffset);
            }

            lastOffset = offset;
        }

        if (anchorConverter.Count == 0)
        {
            return null;
        }

        return anchorConverter;
    }

    private FrameOffsetData? FrameOffset(int frame)
    {
        if (!ThisListView.TryGetItemRect(frame, out RectF8 rect))
        {
            return null;
        }

        double parallelOffset = _isVertical ? rect.Y : rect.X;
        double perpendicularOffset = _isVertical ? rect.X : rect.Y;
        double frameParallelLength = _isVertical ? rect.Height : rect.Width;
        double framePerpendicularLength = _isVertical ? rect.Width : rect.Height;

        var result = new FrameOffsetData
        {
            ParallelStart = parallelOffset,
            ParallelEnd = parallelOffset + frameParallelLength,
            PerpendicularCenter = perpendicularOffset + framePerpendicularLength * 0.5,
        };

        Logger.Assert(double.IsFinite(result.ParallelStart), "B3852325B440B619");
        Logger.Assert(double.IsFinite(result.ParallelEnd), "FA97F0CF86C7DE35");
        Logger.Assert(double.IsFinite(result.PerpendicularCenter), "1C006026686551CB");

        return result;
    }

    #endregion

    #region Reader Status

    private void UpdateReaderStatusText()
    {
        string text = string.Empty;
        if (_isAutoScrolling)
        {
            text = StringResourceProvider.Instance.Auto;
            if (!_isContinuous)
            {
                double targetDelay = 1.0 / Math.Abs(_autoScrollParallelVelocity);
                targetDelay = Math.Round(targetDelay, 1, MidpointRounding.AwayFromZero);
                text += $" ({targetDelay:0.#}s)";
            }
        }

        if (string.IsNullOrEmpty(text))
        {
            ReaderStatusTextBlock.Visibility = Visibility.Collapsed;
        }
        else
        {
            ReaderStatusTextBlock.Text = text;
            ReaderStatusTextBlock.Visibility = Visibility.Visible;
        }
    }

    #endregion

    #region Internal State Configs

    private void SaveZoomingConfig()
    {
        ReaderViewDatabase? db = _internalDB;
        if (db is null)
        {
            return;
        }

        int frameIdx = SCCurrentFrameIndexFinal;
        if (frameIdx < 0 || frameIdx >= _frameItemsSource.Count)
        {
            return;
        }

        ReaderFrameViewModel frameModel = _frameItemsSource[frameIdx];
        ZoomCoefficient? zoomCoefficient = CalculateZoomCoefficient(frameModel);
        if (zoomCoefficient is null)
        {
            return;
        }

        if (_isContinuous)
        {
            if (_isVertical)
            {
                double zooming = SCZoomFactorFinal / zoomCoefficient.FitWidth;
                if (frameModel.IsDualPage)
                {
                    zooming /= DUAL_FRAME_DEFAULT_WIDTH_MULTIPLIER;
                }

                db.FitWidthDualAwareZooming = zooming;
            }
            else
            {
                double zooming = SCZoomFactorFinal / zoomCoefficient.FitHeight;
                db.FitHeightZooming = zooming;
            }
        }
        else
        {
            db.CenterInsideZooming = SCZoomFactorFinal / zoomCoefficient.Min();
        }
    }

    private void LoadZoomingConfig(out float zoom, out ZoomType zoomType)
    {
        zoom = _zoom;
        zoomType = ZoomType.CenterInside;

        ReaderViewDatabase? db = _internalDB;
        if (db is null)
        {
            return;
        }

        if (!_isContinuous)
        {
            if (db.CenterInsideZooming.HasValue)
            {
                zoom = (float)db.CenterInsideZooming.Value;
            }

            return;
        }

        double? savedZooming = _isVertical ? db.FitWidthDualAwareZooming : db.FitHeightZooming;
        if (savedZooming is null)
        {
            return;
        }

        zoom = (float)savedZooming.Value;
        zoomType = _isVertical ? ZoomType.FitWidthDualAware : ZoomType.FitHeight;
    }

    #endregion

    #region Utilities

    private void DispatchReaderStateChangeEvent(ReaderState state, string stateDescription = "")
    {
        if (state == _state && state == ReaderState.Ready)
        {
            return;
        }

        if (state == ReaderState.Ready)
        {
            stateDescription = string.Empty;
        }

        _state = state;
        ContentScrollViewer.Opacity = state == ReaderState.Ready ? 1 : 0;

        ReaderEventReaderStateChanged?.Invoke(this, state, stateDescription);
    }

    private int ToDiscretePage(double pageContinuous)
    {
        return Math.Max(1, Math.Min(PageCount, (int)Math.Round(pageContinuous)));
    }

    private void ConvertOffset(ref double? toHorizontal, ref double? toVertical, double? fromParallel, double? fromPerpendicular)
    {
        if (_isVertical)
        {
            if (fromParallel != null)
            {
                toVertical = fromParallel;
            }

            if (fromPerpendicular != null)
            {
                toHorizontal = fromPerpendicular;
            }
        }
        else
        {
            if (fromParallel != null)
            {
                toHorizontal = fromParallel;
            }

            if (fromPerpendicular != null)
            {
                toVertical = fromPerpendicular;
            }
        }
    }

    private bool TryConvertPageToFrameIndex(int page, out int frameIndex)
    {
        if (page <= 0 || page > PageCount)
        {
            frameIndex = -1;
            return false;
        }

        PageModel? pageModel = _pageModels[page - 1];
        if (pageModel is null)
        {
            frameIndex = -1;
            return false;
        }

        if (pageModel.LayoutInfo is null)
        {
            frameIndex = -1;
            return false;
        }

        frameIndex = pageModel.LayoutInfo.FrameIndex;
        return true;
    }

    private static void Log(string tag, params object?[] values)
    {
        Logger.I(LogTag.N(TAG, tag), string.Join(',', values));
    }

    private static void PostToCurrentThread(Action action, int delayMilliseconds = 0)
    {
        CoroutineUtils.Run(async () =>
        {
            await Task.Delay(delayMilliseconds + 1);
            action();
        });
    }

    private static long GetTicks()
    {
        return Environment.TickCount64;
    }

    #endregion

    #region Types

    public enum ReaderState
    {
        Idle,
        Ready,
        Loading,
        Error,
    }

    public interface IConfigurationDatabase
    {
        string? ReadConfiguration(string key);

        void WriteConfiguration(string key, string value);
    }

    private class PageModel
    {
        public required ReaderImageSource Image { get; set; }
        public required int OriginalWidth { get; set; }
        public required int OriginalHeight { get; set; }

        public PageLayoutInfo? LayoutInfo { get; set; }

        public double AspectRatio
        {
            get
            {
                if (OriginalWidth > 0 && OriginalHeight > 0)
                {
                    return (double)OriginalWidth / OriginalHeight;
                }

                return 0;
            }
        }
    }

    private class GestureHandler(ReaderView view) : ReaderGestureRecognizer.IHandler
    {
        private readonly ReaderView _view = view;

        public void ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            _view.OnReaderManipulationCompleted(sender, e);
        }

        public void ManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
            _view.OnReaderManipulationStarted(sender, e);
        }

        public void ManipulationUpdated(object sender, ManipulationUpdatedEventArgs e)
        {
            _view.OnReaderManipulationUpdated(sender, e);
        }

        public void Tapped(object sender, TappedEventArgs e)
        {
            _view.OnReaderTapped(sender, e);
        }
    }

    private enum ScrollSource
    {
        User,
        UserPrecise,
        Programmatic,
        AutoScroll,
    }

    private enum ScrollResult
    {
        None,
        Success,
        UnknownFailure,
        TooClose,
        Unchanged,
    }

    private enum ZoomType
    {
        CenterInside,
        CenterCrop,
        FitWidthDualAware,
        FitHeight,
    }

    private enum PointerEventType
    {
        Pressed,
        Moved,
        Released,
        Cancelled,
    }

    private class ScrollRequest(ScrollSource source)
    {
        public readonly ScrollSource Source = source;

        // Zoom
        public float? Zoom = null;
        public ZoomType ZoomType = ZoomType.CenterInside;
        public int? FrameIndex = null;

        // Offset
        public double? HorizontalOffset = null;
        public double? VerticalOffset = null;

        // Animation
        public bool DisableAnimation = false;

        // Options
        public bool IgnoreTooClose = false;
    }

    private class ScrollContext
    {
        public ScrollResult Result = ScrollResult.None;
        public float? Zoom = null;
        public float? ZoomFactor = null;
        public double? HorizontalOffset = null;
        public double? VerticalOffset = null;
        public bool DisableAnimation = false;
        public double ScrollAmount = 0.0;
        public double OverScrollAmount = 0.0;
    }

    private class PengingImageItem
    {
        public required int Page;
        public required int OriginalWidth;
        public required int OriginalHeight;
        public required IImageSource Source;
    }

    #endregion
}
