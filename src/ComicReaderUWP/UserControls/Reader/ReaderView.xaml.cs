// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Threading;
using ComicReaderUWP.SDK.Common.Utils;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace ComicReaderUWP.UserControls.Reader;

internal partial class ReaderView : UserControl
{
    #region Constants

    private const string TAG = nameof(ReaderView);
    private const float MAX_ZOOM = 2.5F;
    private const float MIN_ZOOM_CENTER_INSIDE = 0.5F;
    private const float MIN_ZOOM_CENTER_CROP = 0.2F;
    private const double DEFAULT_VERTICAL_PAGE_SPACING = 10.0;
    private const double DEFAULT_HORIZONTAL_PAGE_SPACING = 100.0;
    private const double DUAL_FRAME_DEFAULT_WIDTH_MULTIPLIER = 2.0;
    private const float FORCE_CONTINUOUS_ZOOM_THRESHOLD = 1.05F;
    private const int PRELOAD_FRAMES_BEFORE = 5;
    private const int PRELOAD_FRAMES_AFTER = 5;
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
    private PageArrangementEnum _pageArrangement = PageArrangementEnum.Single;
    private bool _useOriginalSize = false;
    private int _pageGap = 100;
    private ImageRotationEnum _imageRotation = ImageRotationEnum.None;
    private bool _imageFlip = false;
    private bool _imageInvert = false;
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

    private double _initialPage = 1.0;
    private ReaderViewInternalDatabase? _internalDB = null;
    private double _minZoomFactor = double.MaxValue;
    private double _maxZoomFactor = double.MinValue;
    private List<IImageSource> _originalDataModel = [];
    private readonly ITaskDispatcher _loadInfoDispatcher = TaskDispatcher.Factory.NewQueue("ReaderViewLoadInfoQueue");
    private readonly ITaskDispatcher _loadImageDispatcher = TaskDispatcher.Factory.NewQueue("ReaderViewLoadImageQueue");
    private readonly ReaderFrameManager _frameManager = new();
    private readonly Dictionary<int, ImageDataModel> _dataModel = [];
    private readonly CancellationSession _reloadSession;

    private ObservableCollection<ReaderFrameViewModel> FrameDataSource { get; } = [];

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

    public int PageCount { get; private set; } = 0;
    public double CurrentPage { get; private set; } = 0.0;
    private int CurrentPageInt => ToDiscretePage(CurrentPage);
    public int CurrentPageDisplay => CurrentPageInt;
    public bool IsVertical => _isVertical;

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

    public int CurrentPagePercentage
    {
        get
        {
            if (PageCount <= 0)
            {
                return 0;
            }

            if (PageToFrame(CurrentPageDisplay, out _, out _) >= FrameDataSource.Count - 1)
            {
                return 100;
            }

            int percentage = (int)Math.Round(100.0 * (CurrentPage - 0.5) / PageCount, MidpointRounding.AwayFromZero);
            return Math.Clamp(percentage, 0, 100);
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

    public bool OverScrollEnabled
    {
        get => _isOverScrollEnabled;
        set => _isOverScrollEnabled = value;
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

        foreach (ReaderFrameViewModel frameModel in FrameDataSource)
        {
            frameModel.Dispose();
        }

        FrameDataSource.Clear();
    }

    public void StartLoadingImages(IEnumerable<IImageSource> images)
    {
        _originalDataModel = [.. images];
        Reload(_originalDataModel);
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

    public void SetPageArrangement(PageArrangementEnum type)
    {
        if (_pageArrangement == type)
        {
            return;
        }

        _pageArrangement = type;
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

    public void SetPageGap(int pageGap)
    {
        if (pageGap == _pageGap)
        {
            return;
        }

        _pageGap = pageGap;
        _uiStateUpdatedNeedReload = true;
        UpdateUI();
    }

    public void SetImageRotation(ImageRotationEnum rotation)
    {
        if (rotation == _imageRotation)
        {
            return;
        }

        _imageRotation = rotation;
        _uiStateUpdatedNeedReload = true;
        UpdateUI();
    }

    public void SetImageFlip(bool flip)
    {
        if (flip == _imageFlip)
        {
            return;
        }

        _imageFlip = flip;
        foreach (ImageDataModel item in _dataModel.Values)
        {
            item.Image.Flip = flip;
        }

        _uiStateUpdatedNeedReloadImages = true;
        UpdateUI();
    }

    public void SetImageInvert(bool invert)
    {
        if (invert == _imageInvert)
        {
            return;
        }

        _imageInvert = invert;
        foreach (ImageDataModel item in _dataModel.Values)
        {
            item.Image.Invert = invert;
        }

        _uiStateUpdatedNeedReloadImages = true;
        UpdateUI();
    }

    public void SetInitialPage(double page)
    {
        _initialPage = Math.Max(0.5, page);
    }

    public void SetCurrentPage(double page)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);

        if (ComicLoaded)
        {
            SetScrollViewer2("SetCurrentPage", ScrollSource.User, page: page);
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
            ContentListView.ItemContainerStyle = (Style)Resources[isVertical ? "VerticalReaderListViewItemStyle" : "HorizontalReaderListViewItemStyle"];
            ContentListView.ItemsPanel = (ItemsPanelTemplate)Resources[isVertical ? "VerticalReaderListViewItemPanelTemplate" : "HorizontalReaderListViewItemPanelTemplate"];

            for (int i = 0; i < FrameDataSource.Count; ++i)
            {
                _frameManager.MarkModelInstanceOutOfDate(i, "OrientationChanged");
            }

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

            needReload = oldFlowDirection != ContentScrollViewer.FlowDirection;
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
            UpdateImages("UIStateUpdatedNeedReloadImages", clear: true);
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
        if (!_isInitialFrameLoaded || FrameDataSource.Count == 0)
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
        InvalidateFrameOffsetCache();
        int lo = 0;
        int hi = FrameDataSource.Count - 1;
        while (lo + 1 < hi)
        {
            int i = (lo + hi) / 2;
            FrameOffsetData? offsets = FrameOffset(i);
            if (!offsets.HasValue)
            {
                hi = i;
                continue;
            }

            if ((offsets.Value.ParallelStart + offsets.Value.ParallelEnd) * 0.5 <= offset)
            {
                lo = i;
            }
            else
            {
                hi = i;
            }
        }

        AnchorConverter? converter = CreateAnchorConverter(lo, hi);
        if (converter is null)
        {
            return false;
        }

        double page = converter.CalculatePage(offset);
        CurrentPage = Math.Min(page, PageCount + 0.5);
        return true;
    }

    private void UpdateImages(string reason, bool clear = false)
    {
        if (!ComicLoaded)
        {
            return;
        }

        int frame = PageToFrame(CurrentPageInt, out _, out _);
        int preloadWindowBegin = Math.Max(frame - PRELOAD_FRAMES_BEFORE, 0);
        int preloadWindowEnd = Math.Min(frame + PRELOAD_FRAMES_AFTER, FrameDataSource.Count - 1);
        Log("LoadImage", $"Reason={reason},P={CurrentPageInt}");

        for (int i = 0; i < FrameDataSource.Count; ++i)
        {
            ReaderFrameViewModel model = FrameDataSource[i];
            if (i < preloadWindowBegin || i > preloadWindowEnd)
            {
                model.SetLeftImageVisibility(false);
                model.SetRightImageVisibility(false);
            }
            else
            {
                model.SetScale(AppSettingsModel.Instance.AntiAliasingEnabled ? SCZoomFactorFinal : double.PositiveInfinity);
            }
        }

        void addToLoaderQueue(int i)
        {
            if (i < 0 || i >= FrameDataSource.Count)
            {
                return;
            }

            ReaderFrameViewModel model = FrameDataSource[i];

            if (clear)
            {
                model.SetLeftImageVisibility(false);
                model.SetRightImageVisibility(false);
            }

            model.SetLeftImageVisibility(true);
            model.SetRightImageVisibility(true);
        }

        int spread = Math.Max(preloadWindowEnd - frame, frame - preloadWindowBegin);
        addToLoaderQueue(frame);
        for (int i = 1; i <= spread; ++i)
        {
            if (frame + i <= preloadWindowEnd)
            {
                addToLoaderQueue(frame + i);
            }

            if (frame - i >= preloadWindowBegin)
            {
                addToLoaderQueue(frame - i);
            }
        }
    }

    #endregion

    #region Loader

    private double InitialPage => Math.Min(_initialPage, PageCount + 0.5);
    private bool ComicLoaded => _isLoaded && PageCount > 0;

    private void Reload(List<IImageSource> images)
    {
        if (images.Count == 0 || _isDestoryed)
        {
            return;
        }

        // Refresh token
        _reloadSession.Next();
        CancellationSession.IToken token = _reloadSession.Token;

        // Reset internal states
        _minZoomFactor = double.MaxValue;
        _maxZoomFactor = double.MinValue;
        _dataModel.Clear();
        PageCount = images.Count;
        CurrentPage = InitialPage;
        SCClearFinalVal("Reload");

        // Reset loader
        int initialFrameIndex = PageToFrame(ToDiscretePage(InitialPage), out bool _, out int _);
        int lastFrameIndex = PageToFrame(PageCount, out bool _, out int _);
        Log("Reload", $"IP={InitialPage},IF={initialFrameIndex},LP={PageCount},LF={lastFrameIndex}");
        ResetLoader();
        _frameManager.ResetReadyIndex();
        _frameManager.SetFrameReadyHandler(delegate (int index)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (index == initialFrameIndex)
            {
                _isInitialFrameLoaded = true;
            }

            if (index == 0)
            {
                _isFirstFrameLoaded = true;
            }

            if (index == lastFrameIndex)
            {
                _isLastFrameLoaded = true;
            }

            UpdateLoader($"FrameReady,i={index}");

            int progress = Math.Min(99, (int)((index + 1) * 100.0 / (initialFrameIndex + 1)));
            DispatchReaderStateChangeEvent(_state, $"{StringResourceProvider.Instance.ReaderStatusLoading} ({progress}%)");
        });

        // Start loading frames
        DispatchReaderStateChangeEvent(ReaderState.Loading, StringResourceProvider.Instance.ReaderStatusLoading);

        for (int i = FrameDataSource.Count - 1; i > lastFrameIndex; --i)
        {
            FrameDataSource[i].Dispose();
            FrameDataSource.RemoveAt(i);
        }

        for (int i = 0; i < FrameDataSource.Count; ++i)
        {
            ReaderFrameViewModel item = FrameDataSource[i];
            item.PageL = ReaderFrameViewModel.NO_PAGE;
            item.PageR = ReaderFrameViewModel.NO_PAGE;
        }

        _loadInfoDispatcher.Submit("ReaderLoadImageInfo", delegate
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
                        SetImageData(item.Index, item.OriginalWidth, item.OriginalHeight, item.Source, lastFrameIndex);
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
                int width = 0;
                int height = 0;
                if (ImageCacheManager.TryGetOriginalDimension(image, out SizeF size))
                {
                    width = (int)Math.Round(size.Width);
                    height = (int)Math.Round(size.Height);
                }

                pendingList.Add(new()
                {
                    Index = i,
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

    private void SetImageData(int index, int originalWidth, int originalHeight, IImageSource source, int lastFrameIndex)
    {
        Logger.Assert(index >= 0, "E55E628AD1456D37");

        ReaderImageSource imageSourceModel = new()
        {
            Source = source,
            Rotation = _imageRotation,
            Flip = _imageFlip,
            Invert = _imageInvert,
        };
        int imageWidth = imageSourceModel.Rotation switch
        {
            ImageRotationEnum.Rotate90 or ImageRotationEnum.Rotate270 => originalHeight,
            _ => originalWidth,
        };
        int imageHeight = imageSourceModel.Rotation switch
        {
            ImageRotationEnum.Rotate90 or ImageRotationEnum.Rotate270 => originalWidth,
            _ => originalHeight,
        };

        ImageDataModel imageModel = new()
        {
            Image = imageSourceModel,
            OriginalWidth = imageWidth,
            OriginalHeight = imageHeight,
        };
        _dataModel[index] = imageModel;

        int frameIndex = PageToFrame(index + 1, out bool leftSide, out int neighbor);
        bool firstFrame = frameIndex == 0;
        bool lastFrame = frameIndex == lastFrameIndex;

        Logger.Assert(frameIndex >= 0, "50AEE34F38D316D0");
        Logger.Assert(neighbor >= -1, "01CA2D7BCADC4663");

        int page = index + 1;
        bool dual = neighbor != ReaderFrameViewModel.NO_PAGE;

        ImageDataModel? neighborModel = null;
        if (dual)
        {
            int neighborIndex = neighbor - 1;
            if (!_dataModel.TryGetValue(neighborIndex, out neighborModel))
            {
                // Neighbor page not loaded yet, wait for next update
                return;
            }
        }

        double verticalPadding = DEFAULT_VERTICAL_PAGE_SPACING;
        double horizontalPadding = DEFAULT_HORIZONTAL_PAGE_SPACING;
        verticalPadding = _isVertical ? verticalPadding : 0;
        horizontalPadding = _isVertical ? 0 : horizontalPadding;
        verticalPadding *= _pageGap / 100.0;
        horizontalPadding *= _pageGap / 100.0;

        double thisImageWidth = 0;
        double thisImageHeight = 0;
        double neighborImageWidth = 0;
        double neighborImageHeight = 0;
        if (_useOriginalSize)
        {
            double totalWidth = imageWidth;
            double maxHeight = imageHeight;
            if (neighborModel is not null)
            {
                totalWidth += neighborModel.OriginalWidth;
                maxHeight = Math.Max(maxHeight, neighborModel.OriginalHeight);
            }

            if (totalWidth < 1 || maxHeight < 1)
            {
                verticalPadding = 0;
                horizontalPadding = 0;
            }
            else
            {
                thisImageWidth = imageWidth;
                thisImageHeight = imageHeight;
                if (neighborModel is not null)
                {
                    neighborImageWidth = neighborModel.OriginalWidth;
                    neighborImageHeight = neighborModel.OriginalHeight;
                }
            }
        }
        else
        {
            double aspectRatio = imageModel.AspectRatio;
            if (neighborModel is not null)
            {
                aspectRatio += neighborModel.AspectRatio;
            }

            if (aspectRatio < 1e-3)
            {
                verticalPadding = 0;
                horizontalPadding = 0;
            }
            else
            {
                double defaultWidth = 500.0;
                double defaultHeight = 300.0;
                if (dual)
                {
                    defaultWidth *= DUAL_FRAME_DEFAULT_WIDTH_MULTIPLIER;
                }

                thisImageHeight = _isVertical ? defaultWidth / aspectRatio : defaultHeight;
                thisImageWidth = thisImageHeight * imageModel.AspectRatio;
                if (neighborModel is not null)
                {
                    neighborImageHeight = thisImageHeight;
                    neighborImageWidth = thisImageHeight * neighborModel.AspectRatio;
                }
            }
        }

        while (frameIndex >= FrameDataSource.Count)
        {
            _frameManager.MarkModelInstanceOutOfDate(frameIndex, "DataAppended");
            FrameDataSource.Add(new ReaderFrameViewModel(_loadImageDispatcher));
        }

        ReaderFrameViewModel item = FrameDataSource[frameIndex];

        Logger.Assert(double.IsFinite(thisImageWidth), $"Invalid image width {thisImageWidth}");
        Logger.Assert(double.IsFinite(thisImageHeight), $"Invalid image height {thisImageHeight}");
        Logger.Assert(double.IsFinite(neighborImageWidth), $"Invalid neighbor image width {neighborImageWidth}");
        Logger.Assert(double.IsFinite(neighborImageHeight), $"Invalid neighbor image height {neighborImageHeight}");
        Logger.Assert(double.IsFinite(horizontalPadding), "B742A59FA82023CD");
        Logger.Assert(double.IsFinite(verticalPadding), "37E400F20758C487");

        double topPadding = _isVertical && firstFrame ? 10000 : verticalPadding;
        double bottomPadding = _isVertical && lastFrame ? 10000 : verticalPadding;
        double startPadding = !_isVertical && firstFrame ? 10000 : horizontalPadding;
        double endPadding = !_isVertical && lastFrame ? 10000 : horizontalPadding;
        item.FrameMargin = _isLeftToRight ?
            new Thickness(startPadding, topPadding, endPadding, bottomPadding) :
            new Thickness(endPadding, topPadding, startPadding, bottomPadding);

        if (leftSide)
        {
            item.LeftImageWidth = thisImageWidth;
            item.LeftImageHeight = thisImageHeight;
            item.RightImageWidth = neighborImageWidth;
            item.RightImageHeight = neighborImageHeight;
            item.PageL = page;
            item.PageR = neighbor;
        }
        else
        {
            item.LeftImageWidth = neighborImageWidth;
            item.LeftImageHeight = neighborImageHeight;
            item.RightImageWidth = thisImageWidth;
            item.RightImageHeight = thisImageHeight;
            item.PageR = page;
            item.PageL = neighbor;
        }

        if (item.PageL != ReaderFrameViewModel.NO_PAGE)
        {
            if (_dataModel.TryGetValue(item.PageL - 1, out ImageDataModel? leftImageModel))
            {
                item.LeftImageSource = leftImageModel.Image;
            }

            Logger.Assert(item.LeftImageSource != null, "A02FF8F8CDE1D47D");
        }
        else
        {
            item.LeftImageSource = null;
        }

        if (item.PageR != ReaderFrameViewModel.NO_PAGE)
        {
            if (_dataModel.TryGetValue(item.PageR - 1, out ImageDataModel? rightImageModel))
            {
                item.RightImageSource = rightImageModel.Image;
            }

            Logger.Assert(item.RightImageSource != null, "FAFB72226C3D1969");
        }
        else
        {
            item.RightImageSource = null;
        }

        UpdateMinMaxZoomFactor(frameIndex);
        item.RebindEntireViewModel();
        _frameManager.MarkModelContentUpdateToDate(frameIndex, "ViewBindByProperty");
    }

    private void ResetLoader()
    {
        Log("Load", "Reset");
        _isInitialFrameLoaded = false;
        _isInitialFrameActionPerformed = false;
        _isInitialFrameJumped = false;
        _isFirstFrameLoaded = false;
        _isLastFrameLoaded = false;
    }

    private void UpdateLoader(string reason)
    {
        if (!_isLoaded)
        {
            return;
        }

        Log("Load", reason);

        bool needDispatchReadyState = false;

        if (_isInitialFrameLoaded && !_isInitialFrameActionPerformed)
        {
            Log("Load", "InitialFrame");
            _isInitialFrameActionPerformed = true;

            LoadZoomingConfig(out float zoom, out ZoomType zoomType);
            ScrollResult scrollResult = SetScrollViewer2("JumpToInitialPage", ScrollSource.Programmatic,
                zoom: zoom, zoomType: zoomType, page: InitialPage);
            Log("Load", $"InitialFrameScroll (result={scrollResult})");
            EnsureInitialPageJumped();

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
        // In some strange cases, ChangeView method completes successfully,
        // but neither the actual offset has changed or ViewChange callback
        // is being triggered.
        // Possible reproducing path: Switch from vertical view to horizontal view.
        // We check the flag periodically to ensure offset has actually changed.
        // If not, try set the offset again.

        CancellationSession.IToken token = _reloadSession.Token;
        CoroutineUtils.Start(async () =>
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

                ScrollResult scrollResult = SetScrollViewer3($"JumpToInitialPageRetry{i}", ScrollSource.Programmatic);
                Log("Load", $"InitialFrameScrollRetry{i} (result={scrollResult})");
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
        if (_isCommitting)
        {
            Log("ViewChanged", "IgnoreCommitting");
            return;
        }

        bool final = !e.IsIntermediate;
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

            double parallelDiff = Math.Abs(ParallelOffset - SCParallelOffsetFinal);
            if (parallelDiff < 10)
            {
                Log("ViewChanged", $"InitialFrameJumped (P={ParallelOffset},PF={SCParallelOffsetFinal})");
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
            OnViewChanged(final);
        }
        finally
        {
            _isViewChanging = false;
        }
    }

    private void OnViewChanged(bool final)
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
                int frame = PageToFrame(SCCurrentPageFinal, out _, out _);
                frame = Math.Max(0, Math.Min(FrameDataSource.Count - 1, frame));
                if (frame < FrameDataSource.Count)
                {
                    double page = FrameDataSource[frame].Page;
                    if (_isContinuous)
                    {
                        // Stick to the vertical center of current frame.
                        SetScrollViewer2("StickToVerticalCenter", ScrollSource.Programmatic,
                            page: page, applyParallelOffset: false, disableAnimation: false);
                    }
                    else
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
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && AppSettingsModel.Instance.AutomaticallyHideCursor)
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
            double velocity = IsVertical ? e.Velocities.Linear.Y : e.Velocities.Linear.X;

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

    private void OnReaderScrollViewerPointerWheelChanged(PointerRoutedEventArgs e)
    {
        PointerPoint pt = e.GetCurrentPoint(null);
        int delta = -pt.Properties.MouseWheelDelta / (int)Windows.Win32.PInvoke.WHEEL_DELTA;

        if (_isContinuous || _zoom > FORCE_CONTINUOUS_ZOOM_THRESHOLD)
        {
            // Continuous scrolling
            Windows.UI.Core.CoreVirtualKeyStates menuState = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu);
            bool verticalScrolling = !menuState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (verticalScrolling && !_isVertical)
            {
                int frame = PageToFrame(SCCurrentPageFinal, out _, out _);
                ZoomCoefficient? zoomCoefficient = CalculateZoomCoefficient(frame);
                if (zoomCoefficient != null)
                {
                    double zoomFitHeight = SCZoomFactorFinal / zoomCoefficient.FitHeight;
                    verticalScrolling = zoomFitHeight > FORCE_CONTINUOUS_ZOOM_THRESHOLD;
                }
                else
                {
                    verticalScrolling = false;
                }
            }

            if (verticalScrolling)
            {
                SetScrollViewer3("ContinuousVerticalScrollingUsingPointerWheel", ScrollSource.User,
                    verticalOffset: SCVerticalOffsetFinal + delta * 140.0, disableAnimation: false);
            }
            else
            {
                SetScrollViewer3("ContinuousHorizontalScrollingUsingPointerWheel", ScrollSource.User,
                    horizontalOffset: SCHorizontalOffsetFinal + delta * 140.0, disableAnimation: false);
            }
        }
        else
        {
            // Page turning
            MoveFrameByUser("PageTurningUsingPointerWheel", delta);
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
            CoroutineUtils.Start(async () =>
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
                if (!_isVertical && !_isLeftToRight)
                {
                    MoveFrameByUser("JumpToPreviousPageUsingRightKey", -1);
                }
                else
                {
                    MoveFrameByUser("JumpToNextPageUsingRightKey", 1);
                }

                break;

            case Windows.System.VirtualKey.Left:
                if (!_isVertical && !_isLeftToRight)
                {
                    MoveFrameByUser("JumpToNextPageUsingLeftKey", 1);
                }
                else
                {
                    MoveFrameByUser("JumpToPreviousPageUsingLeftKey", -1);
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

    #region Content Change Event Handlers

    private void OnReaderContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        var item = args.Item as ReaderFrameViewModel;
        var viewHolder = args.ItemContainer.ContentTemplateRoot as ReaderFrame;

        if (viewHolder == null)
        {
            return;
        }

        if (args.InRecycleQueue)
        {
            _frameManager.MarkViewNotReady(args.ItemIndex, "ViewRecycled");
            viewHolder.SetReadyStateChangeHandler(null);
            viewHolder.Bind(null);
        }
        else
        {
            int index = args.ItemIndex;

            viewHolder.SetReadyStateChangeHandler(delegate (FrameworkElement container, bool isReady, string reason)
            {
                if (isReady)
                {
                    _frameManager.MarkViewReady(index, container, "ViewReady");
                }
                else
                {
                    _frameManager.MarkViewNotReady(index, "ViewNotReady");
                }
            });

            viewHolder.Bind(item);
            _frameManager.MarkModelInstanceUpdateToDate(index, "ViewBindByContainer");
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

            if (_internalDB is not null)
            {
                _internalDB.AutoScrollVelocity = velocityValue;
            }
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
        Log("AutoScroll", $"Start velocity=({parallelVelocity},{perpendicularVelocity})");
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
                Log("AutoScroll", $"Stop");
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

                int frameIndex = PageToFrame(SCCurrentPageFinal, out _, out _);
                if (frameIndex >= 0 && frameIndex < FrameDataSource.Count)
                {
                    ReaderFrameViewModel frame = FrameDataSource[frameIndex];
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

    private bool _isOverScrollEnabled = false;
    private bool _overScrollStarted = false;
    private double _overScrollAmount = 0.0;

    private void UpdateOverScrollAmount(double increment)
    {
        if (!_isOverScrollEnabled)
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
        if (!_isOverScrollEnabled)
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

    private ScrollViewer ThisScrollViewer => ContentScrollViewer;
    private ListView ThisListView => ContentListView;
    private float ZoomFactor => ThisScrollViewer.ZoomFactor;
    private double HorizontalOffset => ThisScrollViewer.HorizontalOffset;
    private double VerticalOffset => ThisScrollViewer.VerticalOffset;
    private double ParallelOffset => IsVertical ? VerticalOffset : HorizontalOffset;
    private double ViewportWidth => ThisScrollViewer.ViewportWidth;
    private double ViewportHeight => ThisScrollViewer.ViewportHeight;
    private double ViewportParallelLength => IsVertical ? ViewportHeight : ViewportWidth;
    private double ViewportPerpendicularLength => IsVertical ? ViewportWidth : ViewportHeight;
    private double ContentPerpendicularLength => IsVertical ? ThisListView.ActualWidth : ThisListView.ActualHeight;

    private int _SCCurrentPageFinal;
    private int SCCurrentPageFinal
    {
        get
        {
            SCSyncFinalVal();
            return _SCCurrentPageFinal;
        }
        set
        {
            SCSyncFinalVal();
            _SCCurrentPageFinal = value;
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
        _SCCurrentPageFinal = CurrentPageInt;
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
        if (FrameDataSource.Count == 0)
        {
            return;
        }

        int frame = PageToFrame(SCCurrentPageFinal, out _, out _);
        frame += increment;

        if (frame >= FrameDataSource.Count)
        {
            DispatchOverScrollEvent(true);
            return;
        }

        if (frame < 0)
        {
            DispatchOverScrollEvent(false);
            return;
        }

        double page = FrameDataSource[frame].Page;
        float? zoom = _zoom > 1.01F ? 1F : null;
        SetScrollViewer2(reason, source, zoom: zoom, page: page, disableAnimation: !AppSettingsModel.Instance.TransitionAnimation);
    }

    private ScrollResult SetScrollViewer1(string reason, ScrollSource source, float? zoom = null,
        double? parallelOffset = null, double? perpendicularOffset = null, bool disableAnimation = true)
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

    private ScrollResult SetScrollViewer2(string reason, ScrollSource source,
        float? zoom = null, ZoomType zoomType = ZoomType.CenterInside, double? page = null,
        bool applyParallelOffset = true, bool disableAnimation = true)
    {
        double? horizontalOffset = null;
        double? verticalOffset = null;

        if (page.HasValue)
        {
            Tuple<double, double>? offsets = PageOffset(page.Value);
            if (offsets is null)
            {
                Log("Jump", $"Failed (offsets is null, p={page.Value})");
                return ScrollResult.UnknownFailure;
            }

            double parallelOffset = offsets.Item1;
            double perpendicularOffset = offsets.Item2;
            ConvertOffset(ref horizontalOffset, ref verticalOffset, applyParallelOffset ? parallelOffset : null, perpendicularOffset);
        }

        return SetScrollViewerInternal(new ScrollRequest(source)
        {
            Zoom = zoom,
            ZoomType = zoomType,
            Page = page,
            HorizontalOffset = horizontalOffset,
            VerticalOffset = verticalOffset,
            DisableAnimation = disableAnimation,
            IgnoreTooClose = _isViewChanging,
        }, reason);
    }

    private ScrollResult SetScrollViewer3(string reason, ScrollSource source,
        float? zoom = null, ZoomType zoomType = ZoomType.CenterInside,
        double? horizontalOffset = null, double? verticalOffset = null,
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
        Log("Jump", "Request:"
            + $" Reason={reason}"
            + $",Src={(int)request.Source}"
            + $",P={request.Page}"
            + $",Z={request.Zoom}"
            + $",H={request.HorizontalOffset}"
            + $",V={request.VerticalOffset}"
            + $",D={request.DisableAnimation}");

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
                if (request.Source == ScrollSource.User || request.Source == ScrollSource.AutoScroll)
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
            Log("Jump", "Failed (not loaded)");
            context.Result = ScrollResult.UnknownFailure;
            return;
        }

        if (_isCommitting)
        {
            Log("Jump", "Failed (is committing)");
            context.Result = ScrollResult.UnknownFailure;
            return;
        }

        Logger.Assert(float.IsFinite(request.Zoom ?? 0), "5D42C4251571A722");
        Logger.Assert(!float.IsNegative(request.Zoom ?? 0), "65075662668EE56D");
        Logger.Assert(double.IsFinite(request.HorizontalOffset ?? 0), "4FD89F79946B8D03");
        Logger.Assert(double.IsFinite(request.VerticalOffset ?? 0), "6678A0ED7D2FEB43");

        if (request.Source == ScrollSource.User)
        {
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

        Log("Jump", "ParamAfterZoom:"
            + $" Z={context.Zoom}"
            + $",ZF={context.ZoomFactor}"
            + $",H={context.HorizontalOffset}"
            + $",V={context.VerticalOffset}"
            + $",D={context.DisableAnimation}");

        AdjustParallelOffset(request, context);

        Logger.Assert(float.IsFinite(context.Zoom ?? 0), "6BC2B5793E12AFA4");
        Logger.Assert(!float.IsNegative(context.Zoom ?? 0), "CF5A68638CB59852");
        Logger.Assert(float.IsFinite(context.ZoomFactor ?? 0), "FF0AD921D9E8BBB0");
        Logger.Assert(!float.IsNegative(context.ZoomFactor ?? 0), "C226B0EBAC496CED");
        Logger.Assert(double.IsFinite(context.HorizontalOffset ?? 0), "A1FF6DDBAD093F79");
        Logger.Assert(double.IsFinite(context.VerticalOffset ?? 0), "C8D35D8BDDF468F8");

        Log("Jump", "ParamAfterFix:"
            + $" Z={context.Zoom}"
            + $",ZF={context.ZoomFactor}"
            + $",H={context.HorizontalOffset}"
            + $",V={context.VerticalOffset}"
            + $",D={context.DisableAnimation}");

        if (request.Page.HasValue)
        {
            SCCurrentPageFinal = ToDiscretePage(request.Page.Value);
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
                Log("Jump", "Cancelled (TooClose)");
                context.Result = ScrollResult.TooClose;
                return;
            }
        }

        ChangeView(context);
        context.Result = ScrollResult.Success;
    }

    private void SetScrollViewerZoom(ScrollRequest request, ScrollContext context)
    {
        // Calculate zoom coefficient for new frame
        int newFrameIndex;
        ReaderFrameViewModel? newFrame = null;
        ZoomCoefficient? zoomCoefficientNew = null;
        {
            int pageNew = request.Page.HasValue ? (int)Math.Round(request.Page.Value) : SCCurrentPageFinal;
            pageNew = Math.Max(1, Math.Min(pageNew, PageCount));

            if (pageNew > PageCount)
            {
                context.Result = ScrollResult.UnknownFailure;
                return;
            }

            newFrameIndex = PageToFrame(pageNew, out _, out _);
            if (newFrameIndex < 0 || newFrameIndex >= FrameDataSource.Count)
            {
                newFrameIndex = 0;
            }

            if (newFrameIndex < FrameDataSource.Count)
            {
                newFrame = FrameDataSource[newFrameIndex];
                zoomCoefficientNew = CalculateZoomCoefficient(newFrame);
                Log("Jump", "Zoom#1:"
                    + $" PN={pageNew}"
                    + $",FN={newFrameIndex}"
                    + $",ZCN={zoomCoefficientNew}");
            }
        }

        if (newFrame is null || zoomCoefficientNew == null)
        {
            context.Zoom = _zoom;
            context.ZoomFactor = null;
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
            int frame = PageToFrame(SCCurrentPageFinal, out _, out _);
            if (frame < 0 || frame >= FrameDataSource.Count)
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

        Log("Jump", "Zoom#2: ",
            $"ZF1={zoomFactorBefore}",
            $"ZF2={zoomFactorAfter}",
            $"Ratio={zoomChangeRatio}",
            $"Pd1={extraPaddingBefore}",
            $"Pd2={extraPaddingAfter}",
            $"VW={halfViewportWidth}",
            $"VH={halfViewportHeight}",
            $"HO={context.HorizontalOffset}",
            $"VO={context.VerticalOffset}");

        if (IsVertical)
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

    private bool ChangeView(ScrollContext context)
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

        bool successful;
        _isCommitting = true;
        try
        {
            successful = ThisScrollViewer.ChangeView(commitHorizontalOffset, commitVerticalOffset, commitZoomFactor, commitDisableAnimation);
        }
        finally
        {
            _isCommitting = false;
        }

        Log("Jump", "Commit:"
        + " Success=" + successful.ToString()
        + ",Z=" + commitZoomFactor.ToString()
        + ",H=" + commitHorizontalOffset.ToString()
        + ",V=" + commitVerticalOffset.ToString()
        + ",D=" + commitDisableAnimation.ToString());

        return successful;
    }

    private void AdjustParallelOffset(ScrollRequest request, ScrollContext context)
    {
        if (FrameDataSource.Count == 0)
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
            Thickness firstFrameMargin = FrameDataSource[0].FrameMargin;
            double frameMarginStart = _isVertical ? firstFrameMargin.Top :
                (_isLeftToRight ? firstFrameMargin.Left : firstFrameMargin.Right);
            double imageStartOffset = frameMarginStart * zoom;
            movementForward = imageStartOffset - screenCenterOffset;
        }

        double? movementBackward = null;
        if (_isLastFrameLoaded)
        {
            // ExtentLength is unreliable, use frame offset instead
            FrameOffsetData? lastFrameOffset = FrameOffset(FrameDataSource.Count - 1);
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
        if (frameIndex < 0 || frameIndex >= FrameDataSource.Count)
        {
            return null;
        }

        return CalculateZoomCoefficient(FrameDataSource[frameIndex]);
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

    private readonly Dictionary<int, FrameOffsetData> _frameOffsetCache = [];

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
        int nearestFrame = PageToFrame(nearestPage, out _, out _);

        InvalidateFrameOffsetCache();
        AnchorConverter? converter = CreateAnchorConverter(nearestFrame, nearestFrame);
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
        if (FrameDataSource.Count == 0 || startFrame > endFrame || startFrame < 0 || endFrame >= FrameDataSource.Count)
        {
            return null;
        }

        var anchorConverter = new AnchorConverter();

        int maxFrame = Math.Min(endFrame + 1, FrameDataSource.Count - 1);
        FrameOffsetData? lastOffset = null;
        for (int frame = Math.Max(startFrame - 1, 0); frame <= maxFrame; frame++)
        {
            FrameOffsetData? offset = FrameOffset(frame);
            if (!offset.HasValue)
            {
                if (frame == maxFrame)
                {
                    break;
                }

                return null;
            }

            ReaderFrameViewModel frameModel = FrameDataSource[frame];
            if (frameModel.IsEmpty)
            {
                return null;
            }

            if (frame == 0)
            {
                anchorConverter.Insert(0.5, offset.Value.ParallelStart);
            }

            if (frame == FrameDataSource.Count - 1)
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
                double midPointPage = (FrameDataSource[frame - 1].MaxPage + frameModel.MinPage) * 0.5;
                anchorConverter.Insert(midPointPage, midPointOffset);
            }

            lastOffset = offset;
        }

        return anchorConverter.Count > 0 ? anchorConverter : null;
    }

    private FrameOffsetData? FrameOffset(int frame)
    {
        if (_frameOffsetCache.TryGetValue(frame, out FrameOffsetData frameOffsetData))
        {
            return frameOffsetData;
        }

        FrameworkElement container = _frameManager.GetContainer(frame);
        if (container == null)
        {
            return null;
        }

        if (frame < 0 || frame >= FrameDataSource.Count)
        {
            return null;
        }

        ReaderFrameViewModel item = FrameDataSource[frame];
        GeneralTransform frameTransform = container.TransformToVisual(ThisListView);
        Windows.Foundation.Point framePosition = frameTransform.TransformPoint(new(0.0, 0.0));

        double parallelOffset = IsVertical ? framePosition.Y : framePosition.X;
        double perpendicularOffset = IsVertical ? framePosition.X : framePosition.Y;
        if (!_isVertical && !_isLeftToRight)
        {
            parallelOffset -= item.FrameMargin.Left + item.FrameWidth + item.FrameMargin.Right;
        }

        double marginStart = _isVertical ? item.FrameMargin.Top :
            (_isLeftToRight ? item.FrameMargin.Left : item.FrameMargin.Right);
        double frameParallelLength = _isVertical ? item.FrameHeight : item.FrameWidth;
        double framePerpendicularLength = _isVertical ? item.FrameWidth : item.FrameHeight;
        var result = new FrameOffsetData
        {
            ParallelStart = parallelOffset + marginStart,
            ParallelEnd = parallelOffset + marginStart + frameParallelLength,
            PerpendicularCenter = perpendicularOffset + framePerpendicularLength * 0.5,
        };

        Logger.Assert(double.IsFinite(result.ParallelStart), "B3852325B440B619");
        Logger.Assert(double.IsFinite(result.ParallelEnd), "FA97F0CF86C7DE35");
        Logger.Assert(double.IsFinite(result.PerpendicularCenter), "1C006026686551CB");

        _frameOffsetCache[frame] = result;
        return result;
    }

    private void InvalidateFrameOffsetCache()
    {
        _frameOffsetCache.Clear();
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
        ReaderViewInternalDatabase? db = _internalDB;
        if (db is null)
        {
            return;
        }

        int frameIdx = PageToFrame(SCCurrentPageFinal, out _, out _);
        if (frameIdx < 0 || frameIdx >= FrameDataSource.Count)
        {
            return;
        }

        ReaderFrameViewModel frameModel = FrameDataSource[frameIdx];
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

        ReaderViewInternalDatabase? db = _internalDB;
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

    private double FrameParallelLength(int i)
    {
        FrameworkElement container = _frameManager.GetContainer(i);

        if (container != null)
        {
            return IsVertical ? container.ActualHeight : container.ActualWidth;
        }

        return 0;
    }

    private int PageToFrame(int page, out bool leftSide, out int neighbor)
    {
        Logger.Assert(int.IsPositive(page), "6A1624FDFE839510");
        Logger.Assert(page <= PageCount, "F8C3257028D32ED3");

        switch (_pageArrangement)
        {
            case PageArrangementEnum.Single:
                leftSide = true;
                neighbor = ReaderFrameViewModel.NO_PAGE;
                return page - 1;
            case PageArrangementEnum.DualCover:
                leftSide = page == 1 || page % 2 == 0;
                neighbor = (page > 1 && (PageCount % 2 == 1 || page < PageCount)) ? (leftSide ? page + 1 : page - 1) : ReaderFrameViewModel.NO_PAGE;
                return page / 2;
            case PageArrangementEnum.DualCoverMirror:
                leftSide = page == PageCount || page % 2 == 1;
                neighbor = (page > 1 && (PageCount % 2 == 1 || page < PageCount)) ? (leftSide ? page - 1 : page + 1) : ReaderFrameViewModel.NO_PAGE;
                return page / 2;
            case PageArrangementEnum.DualNoCover:
                leftSide = page % 2 == 1;
                neighbor = (PageCount % 2 == 0 || page < PageCount) ? (leftSide ? page + 1 : page - 1) : ReaderFrameViewModel.NO_PAGE;
                return (page - 1) / 2;
            case PageArrangementEnum.DualNoCoverMirror:
                leftSide = page == PageCount || page % 2 == 0;
                neighbor = (PageCount % 2 == 0 || page < PageCount) ? (leftSide ? page - 1 : page + 1) : ReaderFrameViewModel.NO_PAGE;
                return (page - 1) / 2;
            default:
                Logger.AssertNotReachHere("734FF3964EFE8681");
                goto case PageArrangementEnum.Single;
        }
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

    private static void Log(string tag, params object?[] values)
    {
        Logger.I(LogTag.N(TAG, tag), string.Join(',', values));
    }

    private static void PostToCurrentThread(Action action, int delayMilliseconds = 0)
    {
        CoroutineUtils.Start(async () =>
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

    private class ImageDataModel
    {
        public required ReaderImageSource Image { get; set; }
        public required int OriginalWidth { get; set; }
        public required int OriginalHeight { get; set; }
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
        Programmatic,
        AutoScroll,
    }

    private enum ScrollResult
    {
        None,
        Success,
        UnknownFailure,
        TooClose,
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
        public double? Page = null;

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
        public required int Index;
        public required int OriginalWidth;
        public required int OriginalHeight;
        public required IImageSource Source;
    }

    #endregion
}
