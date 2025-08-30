// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Imaging;
using ComicReader.Common.Legacy;
using ComicReader.Common.Threading;
using ComicReader.Common.Utils;
using ComicReader.Data.Models;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Threading;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;

namespace ComicReader.Views.Pages.Reader;

internal partial class ReaderView : UserControl
{
    //
    // Constants
    //

    private const string TAG = nameof(ReaderView);
    private const float MAX_ZOOM = 250F;
    private const float MIN_ZOOM_CENTER_INSIDE = 50F;
    private const float MIN_ZOOM_CENTER_CROP = 20F;
    private const double DEFAULT_VERTICAL_PAGE_SPACING = 10.0;
    private const double DEFAULT_HORIZONTAL_PAGE_SPACING = 100.0;
    private const float FORCE_CONTINUOUS_ZOOM_THRESHOLD = 105F;
    private const int PRELOAD_FRAMES_BEFORE = 10;
    private const int PRELOAD_FRAMES_AFTER = 10;

    //
    // Variables
    //

    private bool _isLoaded;
    private ReaderState _state = ReaderState.Idle;
    private bool _isVertical = true;
    private bool _isContinuous = true;
    private bool _isVisible = true;
    private bool _isLeftToRight = true;
    private PageArrangementEnum _pageArrangement = PageArrangementEnum.Single;
    private bool _useOriginalSize = false;
    private int _pageGap = 100;
    private bool _uiStateUpdatedVisibility = true;
    private bool _uiStateUpdatedOrientation = true;
    private bool _uiStateUpdatedContinuous = true;
    private bool _uiStateUpdatedFlowDirection = true;
    private bool _uiStateUpdatedPageArrangement = true;
    private bool _uiStateUpdatedUseOriginalSize = true;
    private bool _uiStateUpdatedPageGap = true;
    private bool _postUiStateUpdated = false;

    private bool _isFirstFrameLoaded = false;
    private bool _isFirstFrameActionPerformed = false;
    private bool _isInitialFrameLoaded = false;
    private bool _isInitialFrameActionPerformed = false;
    private bool _isInitialFrameJumped = false;
    private bool _isLastFrameLoaded = false;
    private bool _isLastFrameActionPerformed = false;

    private bool _tapPending = false;
    private bool _tapCancelled = false;
    private bool _manipulationDisabled = false;
    private readonly UIElement _gestureReference;
    private readonly GestureHandler _gestureHandler;
    private readonly ReaderGestureRecognizer _gestureRecognizer = new();

    private double _initialPage = 0.0;
    private double _minZoomFactor = double.MaxValue;
    private double _maxZoomFactor = double.MinValue;
    private bool _isViewChanging = false;
    private List<IImageSource> _originalDataModel = [];
    private readonly ITaskDispatcher _loadInfoDispatcher = TaskDispatcher.Factory.NewQueue("ReaderViewLoadInfoQueue");
    private readonly ITaskDispatcher _loadImageDispatcher = TaskDispatcher.Factory.NewQueue("ReaderViewLoadImageQueue");
    private readonly ReaderFrameManager _frameManager = new();
    private readonly ReaderImagePool _imagePool;
    private readonly Dictionary<int, ImageDataModel> _dataModel = [];
    private readonly CancellationSession _dataModelSession;

    private ObservableCollection<ReaderFrameViewModel> FrameDataSource { get; } = [];

    //
    // Constructor
    //

    public ReaderView()
    {
        InitializeComponent();

        Loaded += OnLoadedOrUnloaded;
        Unloaded += OnLoadedOrUnloaded;

        _gestureReference = this;
        _gestureHandler = new(this);
        _gestureRecognizer.SetHandler(_gestureHandler);

        _dataModelSession = new();
        _imagePool = new(_loadImageDispatcher);
    }

    //
    // Public Interfaces
    //

    public delegate void ReaderEventTappedEventHandler(ReaderView sender);
    public event ReaderEventTappedEventHandler? ReaderEventTapped;

    public delegate void ReaderEventPageChangedEventHandler(ReaderView sender, bool isIntermediate);
    public event ReaderEventPageChangedEventHandler? ReaderEventPageChanged;

    public delegate void ReaderEventReaderStateChangeHandler(ReaderView sender, ReaderState state, string description);
    public event ReaderEventReaderStateChangeHandler? ReaderEventReaderStateChanged;

    public int PageCount { get; private set; } = 0;
    public double CurrentPage { get; private set; } = 0.0;
    private int CurrentPageInt => ToDiscretePage(CurrentPage);
    public int CurrentPageDisplay => CurrentPageInt;
    public bool IsLastPage => PageToFrame(CurrentPageDisplay, out _, out _) >= FrameDataSource.Count - 1;
    public bool IsVertical => _isVertical;

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

    public void SetVisibility(bool visible)
    {
        if (visible == _isVisible)
        {
            return;
        }

        _isVisible = visible;
        _uiStateUpdatedVisibility = true;
        UpdateUI();
    }

    public void SetPageArrangement(PageArrangementEnum type)
    {
        if (_pageArrangement == type)
        {
            return;
        }

        _pageArrangement = type;
        _uiStateUpdatedPageArrangement = true;
        UpdateUI();
    }

    public void SetUseOriginalSize(bool useOriginalSize)
    {
        if (useOriginalSize == _useOriginalSize)
        {
            return;
        }

        _uiStateUpdatedUseOriginalSize = true;
        _useOriginalSize = useOriginalSize;
        UpdateUI();
    }

    public void SetPageGap(int pageGap)
    {
        if (pageGap == _pageGap)
        {
            return;
        }

        _pageGap = pageGap;
        _uiStateUpdatedPageGap = true;
        UpdateUI();
    }

    public void SetInitialPage(double page)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        _initialPage = page;
    }

    public void StartLoadingImages(IEnumerable<IImageSource> images)
    {
        _originalDataModel = [.. images];
        Reload(_originalDataModel);
    }

    //
    // Loader
    //

    private double InitialPage => Math.Min(_initialPage, PageCount);
    private bool ComicLoaded => _isLoaded && PageCount > 0;

    private void Reload(List<IImageSource> images)
    {
        if (images.Count == 0)
        {
            return;
        }

        // Refresh token
        _dataModelSession.Next();
        CancellationSession.IToken token = _dataModelSession.Token;

        // Update internal states
        _minZoomFactor = double.MaxValue;
        _maxZoomFactor = double.MinValue;
        _dataModel.Clear();
        PageCount = images.Count;

        int lastFrameIndex = PageToFrame(PageCount, out bool _, out int _);
        for (int i = FrameDataSource.Count - 1; i > lastFrameIndex; --i)
        {
            FrameDataSource.RemoveAt(i);
        }

        _imagePool.Cancel();
        for (int i = 0; i < FrameDataSource.Count; ++i)
        {
            ReaderFrameViewModel item = FrameDataSource[i];
            item.PageL = -1;
            item.PageR = -1;
        }

        SCClearFinalVal("Reload");

        // Reset loader
        int initialFrameIndex = PageToFrame(ToDiscretePage(InitialPage), out bool _, out int _);
        Log("Reload", $"IP={InitialPage},IF={initialFrameIndex},LP={PageCount},LF={lastFrameIndex}");
        ResetLoader();
        _frameManager.ResetReadyIndex();
        _frameManager.SetFrameReadyHandler(delegate (int index)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (index == 0)
            {
                _isFirstFrameLoaded = true;
            }

            if (index == initialFrameIndex)
            {
                _isInitialFrameLoaded = true;
            }

            if (index == lastFrameIndex)
            {
                _isLastFrameLoaded = true;
            }

            UpdateLoader($"FrameReady,i={index}");

            int progress = Math.Min(99, (int)((index + 1) * 100.0 / (initialFrameIndex + 1)));
            DispatchReaderStateChangeEvent(_state, $"{StringResourceProvider.Instance.ReaderStatusLoading} ({progress}%)");
        });

        // Start loading images
        DispatchReaderStateChangeEvent(ReaderState.Loading, StringResourceProvider.Instance.ReaderStatusLoading);
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
                _ = MainThreadUtils.RunInMainThread(delegate
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    foreach (PengingImageItem item in pendingListCopy)
                    {
                        SetImageData(item.Index, item.OriginalWidth, item.OriginalHeight, item.Source);
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
                ImageCacheManager.ImageMeta? imageMeta = ImageCacheManager.GetImageMeta(image);
                int width = 0;
                int height = 0;
                if (imageMeta is not null)
                {
                    width = imageMeta.Width;
                    height = imageMeta.Height;
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

    private void ResetLoader()
    {
        Log("Load", "Reset");
        _isFirstFrameLoaded = false;
        _isFirstFrameActionPerformed = false;
        _isInitialFrameLoaded = false;
        _isInitialFrameActionPerformed = false;
        _isLastFrameLoaded = false;
        _isLastFrameActionPerformed = false;
    }

    private void UpdateLoader(string reason)
    {
        if (!_isLoaded)
        {
            return;
        }

        Log("Load", reason);

        bool needAdjustPadding = false;

        if (_isFirstFrameLoaded && !_isFirstFrameActionPerformed)
        {
            Log("Load", "FirstFrame");
            _isFirstFrameActionPerformed = true;
            needAdjustPadding = true;
        }

        if (_isLastFrameLoaded && !_isLastFrameActionPerformed)
        {
            Log("Load", "LastFrame");
            _isLastFrameActionPerformed = true;
            needAdjustPadding = true;
        }

        if (needAdjustPadding)
        {
            AdjustPadding();
        }

        bool needDispatchReadyState = false;

        if (_isInitialFrameLoaded && !_isInitialFrameActionPerformed)
        {
            Log("Load", "InitialFrame");
            _isInitialFrameActionPerformed = true;

            PostToCurrentThread(delegate
            {
                ScrollResult scrollResult = SetScrollViewer2("JumpToInitialPage", zoom: _zoom, page: InitialPage, disableAnimation: true);
                _isInitialFrameJumped = true;
                Log("Load", $"InitialFrameScroll (result={scrollResult})");
                if (scrollResult == ScrollResult.TooClose)
                {
                    UpdateImages("InitialFrameLoaded");
                }
            });

            needDispatchReadyState = true;
        }

        if (needDispatchReadyState)
        {
            DispatchReaderStateChangeEvent(ReaderState.Ready);
        }
    }

    //
    // UI
    //

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

        if (_uiStateUpdatedVisibility)
        {
            _uiStateUpdatedVisibility = false;
            bool isVisible = _isVisible;
            SvReader.IsEnabled = isVisible;
            SvReader.IsHitTestVisible = isVisible;
            SvReader.Opacity = isVisible ? 1 : 0;
        }

        if (_uiStateUpdatedOrientation)
        {
            _uiStateUpdatedOrientation = false;
            bool isVertical = _isVertical;
            SvReader.VerticalScrollMode = isVertical ? ScrollMode.Enabled : ScrollMode.Disabled;
            GReader.VerticalAlignment = isVertical ? VerticalAlignment.Top : VerticalAlignment.Center;
            GReader.HorizontalAlignment = isVertical ? HorizontalAlignment.Center : HorizontalAlignment.Center;
            LvReader.VerticalAlignment = isVertical ? VerticalAlignment.Top : VerticalAlignment.Center;
            LvReader.HorizontalAlignment = isVertical ? HorizontalAlignment.Center : HorizontalAlignment.Center;
            LvReader.ItemContainerStyle = (Style)Resources[isVertical ? "VerticalReaderListViewItemStyle" : "HorizontalReaderListViewItemStyle"];
            LvReader.ItemsPanel = (ItemsPanelTemplate)Resources[isVertical ? "VerticalReaderListViewItemPanelTemplate" : "HorizontalReaderListViewItemPanelTemplate"];

            for (int i = 0; i < FrameDataSource.Count; ++i)
            {
                _frameManager.MarkModelInstanceOutOfDate(i, "OrientationChanged");
            }

            needReload = true;
        }

        if (_uiStateUpdatedFlowDirection)
        {
            _uiStateUpdatedFlowDirection = false;
            if (_isVertical)
            {
                SvReader.FlowDirection = FlowDirection.LeftToRight;
            }
            else
            {
                SvReader.FlowDirection = _isLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            }
        }

        if (_uiStateUpdatedContinuous)
        {
            _uiStateUpdatedContinuous = false;
            _gestureRecognizer.AutoProcessInertia = _isContinuous;
        }

        if (_uiStateUpdatedPageArrangement)
        {
            _uiStateUpdatedPageArrangement = false;
            needReload = true;
        }

        if (_uiStateUpdatedUseOriginalSize)
        {
            _uiStateUpdatedUseOriginalSize = false;
            needReload = true;
        }

        if (_uiStateUpdatedPageGap)
        {
            _uiStateUpdatedPageGap = false;
            needReload = true;
        }

        if (needReload && _originalDataModel != null)
        {
            if (_isInitialFrameJumped)
            {
                _initialPage = CurrentPage;
            }

            Reload(_originalDataModel);
        }
    }

    private void SetImageData(int index, int originalWidth, int originalHeight, IImageSource source)
    {
        Logger.Assert(index >= 0, "E55E628AD1456D37");

        var model = new ImageDataModel
        {
            ImageSource = source,
            OriginalWidth = originalWidth,
            OriginalHeight = originalHeight,
        };
        _dataModel[index] = model;

        int frameIndex = PageToFrame(index + 1, out bool leftSide, out int neighbor);

        Logger.Assert(frameIndex >= 0, "50AEE34F38D316D0");
        Logger.Assert(neighbor >= -1, "01CA2D7BCADC4663");

        int page = index + 1;
        bool dual = neighbor != -1;

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

        double imageWidth = 0;
        double imageHeight = 0;
        double neighborImageWidth = 0;
        double neighborImageHeight = 0;
        if (_useOriginalSize)
        {
            double totalWidth = originalWidth;
            double maxHeight = originalHeight;
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
                imageWidth = originalWidth;
                imageHeight = originalHeight;
                if (neighborModel is not null)
                {
                    neighborImageWidth = neighborModel.OriginalWidth;
                    neighborImageHeight = neighborModel.OriginalHeight;
                }
            }
        }
        else
        {
            double aspectRatio = model.AspectRatio;
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
                    defaultWidth *= 2;
                }

                imageHeight = _isVertical ? defaultWidth / aspectRatio : defaultHeight;
                imageWidth = imageHeight * model.AspectRatio;
                if (neighborModel is not null)
                {
                    neighborImageHeight = imageHeight;
                    neighborImageWidth = imageHeight * neighborModel.AspectRatio;
                }
            }
        }

        while (frameIndex >= FrameDataSource.Count)
        {
            _frameManager.MarkModelInstanceOutOfDate(frameIndex, "DataAppended");
            FrameDataSource.Add(new ReaderFrameViewModel(_imagePool));
        }

        ReaderFrameViewModel item = FrameDataSource[frameIndex];

        Logger.Assert(double.IsFinite(imageWidth), $"Invalid image width {imageWidth}");
        Logger.Assert(double.IsFinite(imageHeight), $"Invalid image height {imageHeight}");
        Logger.Assert(double.IsFinite(neighborImageWidth), $"Invalid neighbor image width {neighborImageWidth}");
        Logger.Assert(double.IsFinite(neighborImageHeight), $"Invalid neighbor image height {neighborImageHeight}");
        Logger.Assert(double.IsFinite(horizontalPadding), "B742A59FA82023CD");
        Logger.Assert(double.IsFinite(verticalPadding), "37E400F20758C487");

        item.FrameMargin = new Thickness(horizontalPadding, verticalPadding, horizontalPadding, verticalPadding);

        if (leftSide)
        {
            item.LeftImageWidth = imageWidth;
            item.LeftImageHeight = imageHeight;
            item.RightImageWidth = neighborImageWidth;
            item.RightImageHeight = neighborImageHeight;
            item.PageL = page;
            item.PageR = neighbor;
        }
        else
        {
            item.LeftImageWidth = neighborImageWidth;
            item.LeftImageHeight = neighborImageHeight;
            item.RightImageWidth = imageWidth;
            item.RightImageHeight = imageHeight;
            item.PageR = page;
            item.PageL = neighbor;
        }

        if (item.PageL != -1)
        {
            if (_dataModel.TryGetValue(item.PageL - 1, out ImageDataModel? imageModel))
            {
                item.LeftImageSource = imageModel.ImageSource;
            }

            Logger.Assert(item.LeftImageSource != null, "A02FF8F8CDE1D47D");
        }
        else
        {
            item.LeftImageSource = null;
        }

        if (item.PageR != -1)
        {
            if (_dataModel.TryGetValue(item.PageR - 1, out ImageDataModel? imageModel))
            {
                item.RightImageSource = imageModel.ImageSource;
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
        if (!_isInitialFrameLoaded)
        {
            Logger.AssertNotReachHere("3EC47459C554E187");
            return false;
        }

        double offset;
        {
            double parallelOffset = SCParallelOffsetFinal;
            double zoomFactor = SCZoomFactorFinal;
            offset = (parallelOffset + ViewportParallelLength * 0.5) / zoomFactor;
        }

        if (FrameDataSource.Count == 0)
        {
            Logger.AssertNotReachHere("9AE769598FEF42CA");
            return false;
        }

        // Locate current frame using binary search
        int begin = 0;
        int end = FrameDataSource.Count - 1;
        FrameOffsetData? frameOffsets = null;

        while (true)
        {
            int i = (begin + end + 1) / 2;
            FrameOffsetData? offsets = FrameOffset(i);

            if (offsets == null)
            {
                if (begin >= end)
                {
                    frameOffsets = null;
                    break;
                }
                end = i - 1;
                continue;
            }

            if (offsets.ParallelBegin < offset)
            {
                begin = i;
                frameOffsets = offsets;
                if (begin >= end)
                {
                    break;
                }
            }
            else
            {
                if (begin >= end)
                {
                    frameOffsets ??= FrameOffset(begin);
                    break;
                }
                end = i - 1;
            }
        }

        if (frameOffsets == null)
        {
            return false;
        }

        ReaderFrameViewModel frame = FrameDataSource[begin];
        if (frame.PageL == -1 && frame.PageR == -1)
        {
            Logger.AssertNotReachHere("E06181918CA281F4");
            return false;
        }

        int pageMin;
        int pageMax;
        if (frame.PageL == -1)
        {
            pageMin = pageMax = frame.PageR;
        }
        else if (frame.PageR == -1)
        {
            pageMin = pageMax = frame.PageL;
        }
        else
        {
            pageMin = Math.Min(frame.PageL, frame.PageR);
            pageMax = Math.Max(frame.PageL, frame.PageR);
        }

        double page;
        if (offset < frameOffsets.ParallelCenter)
        {
            double pageFrac = (offset - frameOffsets.ParallelBegin) / (frameOffsets.ParallelCenter - frameOffsets.ParallelBegin);
            page = pageMin - 0.5 + pageFrac * 0.5;
        }
        else
        {
            double pageFrac = (offset - frameOffsets.ParallelCenter) / (frameOffsets.ParallelEnd - frameOffsets.ParallelCenter);
            page = pageMax + pageFrac * 0.5;
        }

        CurrentPage = Math.Min(page, PageCount);

        Log("PageUpdated",
            $"P={CurrentPage}," +
            $"PO={ParallelOffset}," +
            $"POF={SCParallelOffsetFinal}," +
            $"Z={ZoomFactor}");

        return true;
    }

    private void UpdateImages(string reason)
    {
        int frame = PageToFrame(CurrentPageInt, out _, out _);
        int preloadWindowBegin = Math.Max(frame - PRELOAD_FRAMES_BEFORE, 0);
        int preloadWindowEnd = Math.Min(frame + PRELOAD_FRAMES_AFTER, FrameDataSource.Count - 1);
        Log("LoadImage", $"reason={reason},P={CurrentPageInt}");

        for (int i = 0; i < FrameDataSource.Count; ++i)
        {
            ReaderFrameViewModel model = FrameDataSource[i];
            if (i < preloadWindowBegin || i > preloadWindowEnd)
            {
                model.LeftImageHolder.SetImage(null);
                model.RightImageHolder.SetImage(null);
            }
            else
            {
                UpdateImageDecodeSize(model);
            }
        }

        void addToLoaderQueue(int i)
        {
            if (i < 0 || i >= FrameDataSource.Count)
            {
                return;
            }

            ReaderFrameViewModel model = FrameDataSource[i];
            model.LeftImageHolder.SetImage(model.LeftImageSource);
            model.RightImageHolder.SetImage(model.RightImageSource);
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

        _imagePool.FlushRequests();
    }

    private void UpdateImageDecodeSize(ReaderFrameViewModel model)
    {
        if (!AppModel.AntiAliasingEnabled)
        {
            return;
        }

        double frameHeight = model.FrameHeight * SCZoomFactorFinal;

        void applyDecodeSize(BitmapImage image)
        {
            if (image == null || image.PixelHeight <= 0 || image.PixelWidth <= 0)
            {
                return;
            }

            double frameWidth = frameHeight * image.PixelWidth / image.PixelHeight;
            double multiplication = 1.2 * DisplayUtils.GetRawPixelPerPixel();
            int decodeHeight = (int)Math.Round(frameHeight * multiplication);
            int decodeWidth = (int)Math.Round(frameWidth * multiplication);

            if (decodeHeight * decodeWidth >= image.PixelHeight * image.PixelWidth)
            {
                decodeHeight = image.PixelHeight;
                decodeWidth = image.PixelWidth;
            }

            if (image.DecodePixelHeight == decodeHeight && image.DecodePixelWidth == decodeWidth)
            {
                return;
            }

            image.DecodePixelWidth = decodeWidth;
            image.DecodePixelHeight = decodeHeight;
        }

        applyDecodeSize(model.ImageLeft);
        applyDecodeSize(model.ImageRight);
    }

    //
    // Load/Unload Handlers
    //

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
        bool lvLoaded = LvReader != null && LvReader.IsLoaded;
        bool svLoaded = SvReader != null && SvReader.IsLoaded;
        bool isLoaded = viewLoaded && lvLoaded && svLoaded;

        if (_isLoaded == isLoaded)
        {
            return;
        }
        _isLoaded = isLoaded;

        if (isLoaded)
        {
            UpdateUI();
            UpdateLoader("Loaded");
        }
        else
        {
            _dataModelSession.Next();
            _imagePool.Cancel();
            DisposeCursor();
        }
    }

    //
    // Size Change Event Handlers
    //

    private void OnReaderScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!ComicLoaded)
        {
            return;
        }

        float zoom = _zoom;
        double page = CurrentPage;

        Log("SizeChanged",
            $"OS=({e.PreviousSize})",
            $"NS=({e.NewSize})",
            $"Z={zoom}",
            $"P={page}",
            $"ZF={ZoomFactor}",
            $"H={HorizontalOffset}",
            $"V={VerticalOffset}");

        AdjustPadding();
        SetScrollViewer2("SizeChanged", zoom: zoom, page: page, disableAnimation: true, fixForPaddingDelay: true);
    }

    //
    // Scroll Event Handlers
    //

    private void OnReaderScrollViewerViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_isCommitting)
        {
            return;
        }

        bool final = !e.IsIntermediate;
        if (final)
        {
            Log("ViewChanged",
                $"Z={ZoomFactor}",
                $"H={HorizontalOffset}",
                $"V={VerticalOffset}");
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
        if (!_isInitialFrameLoaded)
        {
            return;
        }

        if (!UpdatePage())
        {
            return;
        }

        if (final)
        {
            SCClearFinalVal("ViewChanged");

            // Notify the scroll viewer to update its inner states.
            SetScrollViewer1(null, null, false, "AdjustInnerStateAfterViewChanged");

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
                        SetScrollViewer2("StickToVerticalCenter", page: page, applyParallelOffset: false, disableAnimation: false);
                    }
                    else
                    {
                        // Stick to the center of current frame.
                        SetScrollViewer2("StickToFrameCenter", page: page, disableAnimation: false);
                    }
                }
            }

            UpdateImages("ViewChanged");
        }

        ReaderEventPageChanged?.Invoke(this, !final);
    }

    //
    // Content Change Event Handlers
    //

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
            viewHolder.SetImageChangeHandler(null);
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
            viewHolder.SetImageChangeHandler(UpdateImageDecodeSize);

            viewHolder.Bind(item);
            _frameManager.MarkModelInstanceUpdateToDate(index, "ViewBindByContainer");
        }
    }

    //
    // Key Down Event Handlers
    //

    private void OnReaderKeyDown(object sender, KeyRoutedEventArgs e)
    {
        bool handled = true;
        switch (e.Key)
        {
            case VirtualKey.Right:
                if (!_isVertical && !_isLeftToRight)
                {
                    MoveFrame(-1, "JumpToPreviousPageUsingRightKey");
                }
                else
                {
                    MoveFrame(1, "JumpToNextPageUsingRightKey");
                }

                break;

            case VirtualKey.Left:
                if (!_isVertical && !_isLeftToRight)
                {
                    MoveFrame(1, "JumpToNextPageUsingLeftKey");
                }
                else
                {
                    MoveFrame(-1, "JumpToPreviousPageUsingLeftKey");
                }

                break;

            case VirtualKey.Up:
                MoveFrame(-1, "JumpToPerviousPageUsingUpKey");
                break;

            case VirtualKey.Down:
                MoveFrame(1, "JumpToNextPageUsingDownKey");
                break;

            case VirtualKey.PageUp:
                MoveFrame(-1, "JumpToPerviousPageUsingPgUpKey");
                break;

            case VirtualKey.PageDown:
                MoveFrame(1, "JumpToNextPageUsingPgDownKey");
                break;

            case VirtualKey.Home:
                ScrollManager.BeginTransaction(this, "JumpToFirstPageUsingHomeKey")
                    .Page(1)
                    .Commit();
                break;

            case VirtualKey.End:
                ScrollManager.BeginTransaction(this, "JumpToLastPageUsingEndKey")
                    .Page(PageCount)
                    .Commit();
                break;

            case VirtualKey.Space:
                MoveFrame(1, "JumpToNextPageUsingSpaceKey");
                break;

            case VirtualKey.R:
                ScrollManager.BeginTransaction(this, "JumpToRandomPageUsingRKey")
                    .Page(Random.Shared.Next(Math.Max(1, PageCount)) + 1)
                    .Commit();
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

    //
    // Pointer Event Handlers
    //

    private void OnReaderPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        PointerPoint pointer_point = e.GetCurrentPoint(_gestureReference);
        _gestureRecognizer.ProcessUpEvent(pointer_point);
        ((UIElement)sender).ReleasePointerCapture(e.Pointer);

        if (!_gestureRecognizer.AutoProcessInertia)
        {
            _gestureRecognizer.CompleteGesture();
        }
    }

    private void OnReaderPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        _gestureRecognizer.ProcessMoveEvents(e.GetIntermediatePoints(_gestureReference));

        if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && AppModel.AutomaticallyHideCursor)
        {
            ShowCursor();
            HideCursorDelayed(3000);
        }
    }

    private void OnReaderPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        ((UIElement)sender).CapturePointer(e.Pointer);
        PointerPoint pointer_point = e.GetCurrentPoint(_gestureReference);
        _gestureRecognizer.ProcessDownEvent(pointer_point);
    }

    private void OnReaderPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        PointerPoint pointer_point = e.GetCurrentPoint(_gestureReference);
        _gestureRecognizer.ProcessUpEvent(pointer_point);
        ((UIElement)sender).ReleasePointerCapture(e.Pointer);

        if (!_gestureRecognizer.AutoProcessInertia)
        {
            _gestureRecognizer.CompleteGesture();
        }
    }

    private void OnReaderScrollViewerPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        // Ctrl key down indicates the user is zooming the page. In that case we shouldn't handle the event.
        CoreVirtualKeyStates ctrl_state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);

        if (ctrl_state.HasFlag(CoreVirtualKeyStates.Down))
        {
            return;
        }

        OnReaderScrollViewerPointerWheelChanged(e);
    }

    private void OnReaderManipulationStarted(object sender, ManipulationStartedEventArgs e)
    {
        OnReaderManipulationStarted(e);
    }

    private void OnReaderManipulationUpdated(object sender, ManipulationUpdatedEventArgs e)
    {
        OnReaderManipulationUpdated(e);
    }

    private void OnReaderManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
    {
        OnReaderManipulationCompleted(e);
    }

    private void OnReaderManipulationStarted(ManipulationStartedEventArgs e)
    {
        _manipulationDisabled = false;
    }

    private void OnReaderManipulationUpdated(ManipulationUpdatedEventArgs e)
    {
        if (_manipulationDisabled)
        {
            return;
        }

        double dx = e.Delta.Translation.X;
        double dy = e.Delta.Translation.Y;
        float scale = e.Delta.Scale;

        if (!_isVertical && !_isLeftToRight)
        {
            dx = -dx;
        }

        float? zoom = null;

        if (Math.Abs(scale - 1.0f) > 0.01f)
        {
            zoom = _zoom * scale;
        }

        ScrollManager.BeginTransaction(this, "ContinuousScrollingUsingManipulation")
            .Zoom(zoom)
            .HorizontalOffset(SCHorizontalOffsetFinal - dx)
            .VerticalOffset(SCVerticalOffsetFinal - dy)
            .EnableAnimation()
            .Commit();
    }

    private void OnReaderManipulationCompleted(ManipulationCompletedEventArgs e)
    {
        if (_isContinuous || _zoom >= FORCE_CONTINUOUS_ZOOM_THRESHOLD)
        {
            return;
        }

        double velocity = IsVertical ? e.Velocities.Linear.Y : e.Velocities.Linear.X;

        if (!_isVertical && !_isLeftToRight)
        {
            velocity = -velocity;
        }

        if (velocity > 1.0)
        {
            MoveFrame(-1, "MoveToLastPageUsingManipulation");
        }
        else if (velocity < -1.0)
        {
            MoveFrame(1, "MoveToNextPageUsingManipulation");
        }
    }

    private void OnReaderScrollViewerPointerWheelChanged(PointerRoutedEventArgs e)
    {
        PointerPoint pt = e.GetCurrentPoint(null);
        int delta = -pt.Properties.MouseWheelDelta / 120;

        if (_isContinuous || _zoom > 105)
        {
            // Continuous scrolling.
            ScrollManager.BeginTransaction(this, "ContinuousScrollingUsingPointerWheel")
                .ParallelOffset(SCParallelOffsetFinal + delta * 140.0)
                .EnableAnimation()
                .Commit();
        }
        else
        {
            // Page turning.
            MoveFrame(delta, "PageTurningUsingPointerWheel");
        }

        _manipulationDisabled = true;
        e.Handled = true;
    }

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
            C0.Run(async delegate
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
            if (Math.Abs(_zoom - 100) <= 1)
            {
                ScrollManager.BeginTransaction(this, "FitScreenUsingCenterCrop")
                    .Zoom(100, ZoomType.CenterCrop)
                    .EnableAnimation()
                    .Commit();
            }
            else
            {
                ScrollManager.BeginTransaction(this, "FitScreenUsingCenterInside")
                    .Zoom(100)
                    .EnableAnimation()
                    .Commit();
            }
        }
    }

    //
    // Cursor
    //

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

        long targetTime = GetTick() + delayMilliseconds;
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

            long currentTime = GetTick();
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

    //
    // Scroll Controller
    //

    private bool _isCommitting = false;
    private float _zoom = 100f;
    private bool _finalValueSynced = false;

    private ScrollViewer ThisScrollViewer => SvReader;
    private ListView ThisListView => LvReader;
    private float ZoomFactor => ThisScrollViewer.ZoomFactor;
    private double HorizontalOffset => ThisScrollViewer.HorizontalOffset;
    private double VerticalOffset => ThisScrollViewer.VerticalOffset;
    private double ParallelOffset => IsVertical ? VerticalOffset : HorizontalOffset;
    private double ViewportWidth => ThisScrollViewer.ViewportWidth;
    private double ViewportHeight => ThisScrollViewer.ViewportHeight;
    private double ViewportParallelLength => IsVertical ? ViewportHeight : ViewportWidth;
    private double ViewportPerpendicularLength => IsVertical ? ViewportWidth : ViewportHeight;
    private double ContentPerpendicularLength => IsVertical ? ThisListView.ActualWidth : ThisListView.ActualHeight;
    private double ExtentParallelLength => IsVertical ? ThisScrollViewer.ExtentHeight : ThisScrollViewer.ExtentWidth;

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

    private double _SCPaddingStartFinal;
    private double SCPaddingStartFinal
    {
        get
        {
            SCSyncFinalVal();
            return _SCPaddingStartFinal;
        }
        set
        {
            SCSyncFinalVal();
            _SCPaddingStartFinal = value;
        }
    }

    private double _SCPaddingEndFinal;
    private double SCPaddingEndFinal
    {
        get
        {
            SCSyncFinalVal();
            return _SCPaddingEndFinal;
        }
        set
        {
            SCSyncFinalVal();
            _SCPaddingEndFinal = value;
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
        _SCPaddingStartFinal = IsVertical ? ThisListView.Padding.Top : ThisListView.Padding.Left;
        _SCPaddingEndFinal = IsVertical ? ThisListView.Padding.Bottom : ThisListView.Padding.Right;
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

    public bool MoveFrame(int increment, string reason)
    {
        MoveFrameInternal(increment, !AppModel.TransitionAnimation, reason);
        return true;
    }

    private void MoveFrameInternal(int increment, bool disableAnimation, string reason)
    {
        if (FrameDataSource.Count == 0)
        {
            return;
        }

        int frame = PageToFrame(SCCurrentPageFinal, out _, out _);
        frame += increment;
        frame = Math.Min(FrameDataSource.Count - 1, frame);
        frame = Math.Max(0, frame);

        double page = FrameDataSource[frame].Page;
        float? zoom = _zoom > 101f ? 100f : null;
        SetScrollViewer2(reason, zoom: zoom, page: page, disableAnimation: disableAnimation);
    }

    private ScrollResult SetScrollViewer1(float? zoom, double? parallelOffset, bool disableAnimation, string reason)
    {
        double? horizontalOffset = _isVertical ? null : parallelOffset;
        double? verticalOffset = _isVertical ? parallelOffset : null;

        return SetScrollViewerInternal(new ScrollRequest
        {
            zoom = zoom,
            horizontalOffset = horizontalOffset,
            verticalOffset = verticalOffset,
            disableAnimation = disableAnimation,
            ignoreTooClose = _isViewChanging,
        }, reason);
    }

    private ScrollResult SetScrollViewer2(string reason, float? zoom = null, double? page = null,
        bool applyParallelOffset = true, bool disableAnimation = false, bool fixForPaddingDelay = false)
    {
        double? horizontalOffset = null;
        double? verticalOffset = null;

        if (page.HasValue)
        {
            Tuple<double, double>? offsets = PageOffset(page.Value);
            if (offsets is null)
            {
                Log("Jump", $"Failed (offsets is null, p={page.Value})");
                return ScrollResult.Failed;
            }

            double parallelOffset = offsets.Item1;
            double perpendicularOffset = offsets.Item2;

            if (fixForPaddingDelay)
            {
                FrameOffsetData? firstFrameOffset = FrameOffset(0);
                if (firstFrameOffset is not null)
                {
                    double desiredPaddingStart = SCPaddingStartFinal;
                    double actualPaddingStart = firstFrameOffset.ParallelBegin;
                    if (Math.Abs(desiredPaddingStart - actualPaddingStart) > 1.0)
                    {
                        double fixingForPaddingDelay = (desiredPaddingStart - actualPaddingStart) * SCZoomFactorFinal;
                        Log("Jump", $"FixingForPaddingDelay={fixingForPaddingDelay}");
                        parallelOffset += fixingForPaddingDelay;
                    }
                }
            }

            ConvertOffset(ref horizontalOffset, ref verticalOffset, applyParallelOffset ? parallelOffset : null, perpendicularOffset);
        }

        return SetScrollViewerInternal(new ScrollRequest
        {
            zoom = zoom,
            page = page,
            horizontalOffset = horizontalOffset,
            verticalOffset = verticalOffset,
            disableAnimation = disableAnimation,
            ignoreTooClose = _isViewChanging,
        }, reason);
    }

    private ScrollResult SetScrollViewer3(
        float? zoom,
        ZoomType zoomType,
        double? horizontalOffset,
        double? verticalOffset,
        bool disableAnimation,
        string reason)
    {
        return SetScrollViewerInternal(new ScrollRequest
        {
            zoom = zoom,
            zoomType = zoomType,
            horizontalOffset = horizontalOffset,
            verticalOffset = verticalOffset,
            disableAnimation = disableAnimation,
            ignoreTooClose = _isViewChanging,
        }, reason);
    }

    private ScrollResult SetScrollViewerInternal(ScrollRequest request, string reason)
    {
        if (!_isLoaded)
        {
            Log("Jump", "Failed (not loaded)");
            return ScrollResult.Failed;
        }

        if (_isCommitting)
        {
            Log("Jump", "Failed (is committing)");
            return ScrollResult.Failed;
        }

        Logger.Assert(float.IsFinite(request.zoom ?? 0), "5D42C4251571A722");
        Logger.Assert(!float.IsNegative(request.zoom ?? 0), "65075662668EE56D");
        Logger.Assert(double.IsFinite(request.horizontalOffset ?? 0), "4FD89F79946B8D03");
        Logger.Assert(double.IsFinite(request.verticalOffset ?? 0), "6678A0ED7D2FEB43");

        Log("Jump", "Request:"
            + $" Reason={reason}"
            + $",P={request.page}"
            + $",Z={request.zoom}"
            + $",H={request.horizontalOffset}"
            + $",V={request.verticalOffset}"
            + $",D={request.disableAnimation}");

        var context = new ScrollContext
        {
            ZoomPercentage = request.zoom,
            DisableAnimation = request.disableAnimation,
            HorizontalOffset = request.horizontalOffset,
            VerticalOffset = request.verticalOffset,
        };

        SetScrollViewerZoom(request, context);

        Logger.Assert(float.IsFinite(context.ZoomPercentage ?? 0), "8E76EB6D567DCCB9");
        Logger.Assert(!float.IsNegative(context.ZoomPercentage ?? 0), "7D83986CC7231EAE");
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
            + $" Z={context.ZoomPercentage}"
            + $",ZF={context.ZoomFactor}"
            + $",H={context.HorizontalOffset}"
            + $",V={context.VerticalOffset}"
            + $",D={context.DisableAnimation}");

        AdjustParallelOffset(context);

        Logger.Assert(float.IsFinite(context.ZoomPercentage ?? 0), "6BC2B5793E12AFA4");
        Logger.Assert(!float.IsNegative(context.ZoomPercentage ?? 0), "CF5A68638CB59852");
        Logger.Assert(float.IsFinite(context.ZoomFactor ?? 0), "FF0AD921D9E8BBB0");
        Logger.Assert(!float.IsNegative(context.ZoomFactor ?? 0), "C226B0EBAC496CED");
        Logger.Assert(double.IsFinite(context.HorizontalOffset ?? 0), "A1FF6DDBAD093F79");
        Logger.Assert(double.IsFinite(context.VerticalOffset ?? 0), "C8D35D8BDDF468F8");

        Log("Jump", "ParamAfterFix:"
            + $" Z={context.ZoomPercentage}"
            + $",ZF={context.ZoomFactor}"
            + $",H={context.HorizontalOffset}"
            + $",V={context.VerticalOffset}"
            + $",D={context.DisableAnimation}");

        if (request.page.HasValue)
        {
            SCCurrentPageFinal = ToDiscretePage(request.page.Value);
        }

        if (context.ZoomPercentage.HasValue)
        {
            _zoom = context.ZoomPercentage.Value;
        }

        if (context.HorizontalOffset == null && context.VerticalOffset == null && context.ZoomFactor == null)
        {
            return ScrollResult.Success;
        }

        if (request.ignoreTooClose)
        {
            double verticalOffsetDiff = context.VerticalOffset.HasValue ? SCVerticalOffsetFinal - context.VerticalOffset.Value : 0.0;
            double horizontalOffsetDiff = context.HorizontalOffset.HasValue ? SCHorizontalOffsetFinal - context.HorizontalOffset.Value : 0.0;
            double zoomFactorDiff = context.ZoomFactor.HasValue ? context.ZoomFactor.Value / Math.Max(SCZoomFactorFinal, 1E-5) : 1.0;
            if (Math.Abs(verticalOffsetDiff) <= 5.0 && Math.Abs(horizontalOffsetDiff) <= 5.0 && Math.Abs(zoomFactorDiff - 1.0) <= 0.01)
            {
                // Ignore the request if target offset is really close to the current offset,
                // otherwise we might trigger a dead loop
                Log("Jump", "Cancelled (too close)");
                return ScrollResult.TooClose;
            }
        }

        ChangeView(context.ZoomFactor, context.HorizontalOffset, context.VerticalOffset, context.DisableAnimation);
        return ScrollResult.Success;
    }

    private void SetScrollViewerZoom(ScrollRequest request, ScrollContext context)
    {
        // Calculate zoom coefficient for new frame
        ZoomCoefficient? zoomCoefficientNew;
        int frameNew;
        {
            int pageNew = request.page.HasValue ? (int)Math.Round(request.page.Value) : SCCurrentPageFinal;
            frameNew = PageToFrame(pageNew, out _, out _);
            if (frameNew < 0 || frameNew >= FrameDataSource.Count)
            {
                frameNew = 0;
            }
            zoomCoefficientNew = CalculateZoomCoefficient(frameNew);
            Log("Jump", "Zoom#1:"
                + $" PN={pageNew}"
                + $",FN={frameNew}"
                + $",ZCN={zoomCoefficientNew}");
            if (zoomCoefficientNew == null)
            {
                context.ZoomPercentage = _zoom;
                context.ZoomFactor = null;
                return;
            }
        }

        // Calculate zoom factor
        double centerCropMultipier = zoomCoefficientNew.Max() / zoomCoefficientNew.Min();
        double zoom;
        if (request.zoom.HasValue)
        {
            zoom = request.zoom.Value;
            if (request.zoomType == ZoomType.CenterCrop)
            {
                zoom *= centerCropMultipier;
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
            if (frame != frameNew)
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

        // Ignore vary less than 1%
        // Commented out because it causes calculation error when dimensions of adjacent frames are close but not equal.
        //if (Math.Abs(zoomFactorNew / SCZoomFactorFinal - 1.0f) <= 0.01f)
        //{
        //    context.ZoomFactor = null;
        //    return;
        //}

        context.ZoomFactor = (float)zoomFactorNew;
        context.ZoomPercentage = (float)zoom;

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

    private bool ChangeView(float? zoomFactor, double? horizontalOffset, double? verticalOffset, bool disableAnimation)
    {
        if (horizontalOffset != null)
        {
            SCHorizontalOffsetFinal = horizontalOffset.Value;
        }

        if (verticalOffset != null)
        {
            SCVerticalOffsetFinal = verticalOffset.Value;
        }

        if (zoomFactor != null)
        {
            SCZoomFactorFinal = zoomFactor.Value;
        }

        if (disableAnimation)
        {
            SCDisableAnimationFinal = true;
        }

        double commitHorizontalOffset = SCHorizontalOffsetFinal;
        double commitVerticalOffset = SCVerticalOffsetFinal;
        float commitZoomFactor = SCZoomFactorFinal;
        bool commitDisableAnimation = SCDisableAnimationFinal;

        bool sucess;
        _isCommitting = true;
        try
        {
            sucess = ThisScrollViewer.ChangeView(commitHorizontalOffset, commitVerticalOffset, commitZoomFactor, commitDisableAnimation);
        }
        finally
        {
            _isCommitting = false;
        }

        Log("Jump", "Commit:"
        + " Success=" + sucess.ToString()
        + ",Z=" + commitZoomFactor.ToString()
        + ",H=" + commitHorizontalOffset.ToString()
        + ",V=" + commitVerticalOffset.ToString()
        + ",D=" + commitDisableAnimation.ToString());

        return sucess;
    }

    private void AdjustParallelOffset(ScrollContext context)
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
        FrameworkElement firstContainer = _frameManager.GetContainer(0);
        if (firstContainer != null)
        {
            double frameParallelLength = _isVertical ? firstContainer.ActualHeight : firstContainer.ActualWidth;
            double space = SCPaddingStartFinal * zoom - parallelOffset;
            double imageCenterOffset = (SCPaddingStartFinal + frameParallelLength * 0.5) * zoom;
            double imageCenterToScreenCenter = imageCenterOffset - screenCenterOffset;
            movementForward = Math.Min(space, imageCenterToScreenCenter);
        }

        double? movementBackward = null;
        FrameworkElement lastContainer = _frameManager.GetContainer(FrameDataSource.Count - 1);
        if (lastContainer != null)
        {
            double frameParallelLength = _isVertical ? lastContainer.ActualHeight : lastContainer.ActualWidth;
            double extentParallelLength = ExtentParallelLength * zoom / ZoomFactor;
            double space = SCPaddingEndFinal * zoom - (extentParallelLength - parallelOffset - ViewportParallelLength);
            double imageCenterOffset = extentParallelLength - (SCPaddingEndFinal + frameParallelLength * 0.5) * zoom;
            double imageCenterToScreenCenter = screenCenterOffset - imageCenterOffset;
            movementBackward = Math.Min(space, imageCenterToScreenCenter);
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

        if (_isVertical)
        {
            context.VerticalOffset += movement;
        }
        else
        {
            context.HorizontalOffset += movement;
        }

        _manipulationDisabled = true;
    }

    private void AdjustPadding()
    {
        if (!_isLoaded)
        {
            return;
        }

        if (FrameDataSource.Count == 0)
        {
            return;
        }

        double paddingStart = SCPaddingStartFinal;
        do
        {
            int frameIdx = 0;
            if (_frameManager.GetContainer(frameIdx) == null)
            {
                break;
            }
            ZoomCoefficient? zoomCoefficient = CalculateZoomCoefficient(frameIdx);
            if (zoomCoefficient == null)
            {
                break;
            }
            double zoomFactor = Math.Min(MIN_ZOOM_CENTER_INSIDE * zoomCoefficient.Min(), MIN_ZOOM_CENTER_CROP * zoomCoefficient.Max());
            zoomFactor = Math.Min(zoomFactor, _minZoomFactor);
            double innerLength = ViewportParallelLength / zoomFactor;
            paddingStart = (innerLength - FrameParallelLength(frameIdx)) / 2;
            paddingStart = Math.Max(0.0, paddingStart);
        } while (false);

        double paddingEnd = SCPaddingEndFinal;
        do
        {
            int frameIdx = FrameDataSource.Count - 1;
            if (_frameManager.GetContainer(frameIdx) == null)
            {
                break;
            }
            ZoomCoefficient? zoomCoefficient = CalculateZoomCoefficient(frameIdx);
            if (zoomCoefficient == null)
            {
                break;
            }
            double zoomFactor = Math.Min(MIN_ZOOM_CENTER_INSIDE * zoomCoefficient.Min(), MIN_ZOOM_CENTER_CROP * zoomCoefficient.Max());
            zoomFactor = Math.Min(zoomFactor, _minZoomFactor);
            double innerLength = ViewportParallelLength / zoomFactor;
            paddingEnd = (innerLength - FrameParallelLength(frameIdx)) / 2;
            paddingEnd = Math.Max(0.0, paddingEnd);
        } while (false);

        SCPaddingStartFinal = paddingStart;
        SCPaddingEndFinal = paddingEnd;
        if (IsVertical)
        {
            ThisListView.Padding = new Thickness(0.0, paddingStart, 0.0, paddingEnd);
        }
        else
        {
            ThisListView.Padding = new Thickness(paddingStart, 0.0, paddingEnd, 0.0);
        }
    }

    private Tuple<double, double>? PageOffset(double page)
    {
        Logger.Assert(double.IsFinite(page), "251D69B9AD4BFDDA");

        int pageInt = (int)Math.Round(page);
        page = Math.Min(page, PageCount);
        pageInt = Math.Min(pageInt, PageCount);
        page = Math.Max(page, 1);
        pageInt = Math.Max(pageInt, 1);

        int frame = PageToFrame(pageInt, out _, out int neighbor);
        FrameOffsetData? offsets = FrameOffset(frame);
        if (offsets == null)
        {
            return null;
        }

        double perpendicularOffset = offsets.PerpendicularCenter * SCZoomFactorFinal - ViewportPerpendicularLength * 0.5;
        // Negative offset indicates that the scrollable content is smaller
        // than the visible area of the ScrollViewer. In that case offset is 0.
        perpendicularOffset = Math.Max(perpendicularOffset, 0.0);

        int pageMin;
        int pageMax;
        if (neighbor == -1)
        {
            pageMin = pageMax = pageInt;
        }
        else
        {
            pageMin = Math.Min(pageInt, neighbor);
            pageMax = Math.Max(pageInt, neighbor);
        }

        double parallelOffset;
        if (pageMin <= page && page <= pageMax)
        {
            parallelOffset = offsets.ParallelCenter;
        }
        else if (page < pageMin)
        {
            double pageFrac = (0.5 - pageMin + page) * 2.0;
            parallelOffset = offsets.ParallelBegin + pageFrac * (offsets.ParallelCenter - offsets.ParallelBegin);
        }
        else
        {
            double pageFrac = (page - pageMax) * 2.0;
            parallelOffset = offsets.ParallelCenter + pageFrac * (offsets.ParallelEnd - offsets.ParallelCenter);
        }

        parallelOffset = parallelOffset * SCZoomFactorFinal - ViewportParallelLength * 0.5;
        var result = new Tuple<double, double>(parallelOffset, perpendicularOffset);

        Logger.Assert(double.IsFinite(result.Item1), "F00BE2F8D9D28D43");
        Logger.Assert(double.IsFinite(result.Item2), "FAD6B4BA580151CF");

        return result;
    }

    private FrameOffsetData? FrameOffset(int frame)
    {
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

        GeneralTransform frame_transform = container.TransformToVisual(ThisListView);
        Point frame_position = frame_transform.TransformPoint(new Point(0.0, 0.0));

        double parallel_offset = IsVertical ? frame_position.Y : frame_position.X;
        double perpendicular_offset = IsVertical ? frame_position.X : frame_position.Y;

        bool left_to_right = _isLeftToRight;

        if (!_isVertical && !left_to_right)
        {
            parallel_offset -= item.FrameMargin.Left + item.FrameWidth + item.FrameMargin.Right;
        }

        var result = new FrameOffsetData
        {
            ParallelBegin = parallel_offset,
            ParallelCenter = parallel_offset + (IsVertical ?
                item.FrameMargin.Top + item.FrameHeight * 0.5 :
                item.FrameMargin.Left + item.FrameWidth * 0.5),
            ParallelEnd = parallel_offset + (IsVertical ?
                item.FrameMargin.Top + item.FrameHeight + item.FrameMargin.Bottom :
                item.FrameMargin.Left + item.FrameWidth + item.FrameMargin.Right),
            PerpendicularCenter = perpendicular_offset + (IsVertical ?
                item.FrameMargin.Left + item.FrameWidth * 0.5 :
                item.FrameMargin.Top + item.FrameHeight * 0.5),
        };

        Logger.Assert(double.IsFinite(result.ParallelEnd), "B3852325B440B619");
        Logger.Assert(double.IsFinite(result.ParallelCenter), "FA97F0CF86C7DE35");
        Logger.Assert(double.IsFinite(result.ParallelBegin), "1C006026686551CB");
        Logger.Assert(double.IsFinite(result.PerpendicularCenter), "A5E16EBC969719EF");
        return result;
    }

    private ZoomCoefficient? CalculateZoomCoefficient(int frameIndex)
    {
        if (FrameDataSource.Count == 0)
        {
            return null;
        }

        if (frameIndex < 0 || frameIndex >= FrameDataSource.Count)
        {
            Logger.AssertNotReachHere("AC9607BD559C90F8");
            return null;
        }

        double viewportWidth = ViewportWidth;
        double viewportHeight = ViewportHeight;
        double frameWidth = FrameDataSource[frameIndex].FrameWidth;
        double frameHeight = FrameDataSource[frameIndex].FrameHeight;

        double minValue = Math.Min(viewportWidth, viewportHeight);
        minValue = Math.Min(minValue, frameWidth);
        minValue = Math.Min(minValue, frameHeight);
        if (minValue < 0.1)
        {
            return null;
        }

        return new ZoomCoefficient
        {
            FitWidth = 0.01 * viewportWidth / frameWidth,
            FitHeight = 0.01 * viewportHeight / frameHeight
        };
    }

    private double CalculateExtraPerpendicularPadding(double zoom)
    {
        double padding = (ViewportPerpendicularLength - ContentPerpendicularLength * zoom) * 0.5;
        return Math.Max(padding, 0);
    }

    //
    // Utilities
    //

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

    private int PageToFrame(int page, out bool left_side, out int neighbor)
    {
        Logger.Assert(int.IsPositive(page), "6A1624FDFE839510");
        Logger.Assert(page <= PageCount, "F8C3257028D32ED3");

        switch (_pageArrangement)
        {
            case PageArrangementEnum.Single:
                left_side = true;
                neighbor = -1;
                return page - 1;
            case PageArrangementEnum.DualCover:
                left_side = page == 1 || page % 2 == 0;
                neighbor = (page > 1 && (PageCount % 2 == 1 || page < PageCount)) ? (left_side ? page + 1 : page - 1) : -1;
                return page / 2;
            case PageArrangementEnum.DualCoverMirror:
                left_side = page == PageCount || page % 2 == 1;
                neighbor = (page > 1 && (PageCount % 2 == 1 || page < PageCount)) ? (left_side ? page - 1 : page + 1) : -1;
                return page / 2;
            case PageArrangementEnum.DualNoCover:
                left_side = page % 2 == 1;
                neighbor = (PageCount % 2 == 0 || page < PageCount) ? (left_side ? page + 1 : page - 1) : -1;
                return (page - 1) / 2;
            case PageArrangementEnum.DualNoCoverMirror:
                left_side = page == PageCount || page % 2 == 0;
                neighbor = (PageCount % 2 == 0 || page < PageCount) ? (left_side ? page - 1 : page + 1) : -1;
                return (page - 1) / 2;
            default:
                Logger.AssertNotReachHere("734FF3964EFE8681");
                goto case PageArrangementEnum.Single;
        }
    }

    private int ToDiscretePage(double pageContinuous)
    {
        return Math.Max(1, (int)Math.Round(pageContinuous));
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

    private static void PostToCurrentThread(Action<Task> action, int delayMilliseconds = 0)
    {
        var context = TaskScheduler.FromCurrentSynchronizationContext();
        _ = Task.Delay(delayMilliseconds + 1).ContinueWith(action, context);
    }

    private static long GetTick()
    {
        return Environment.TickCount64;
    }

    //
    // Classes
    //

    internal sealed class ScrollManager : BaseTransaction<ScrollResult>
    {
        private readonly WeakReference<ReaderView> _reader;
        private readonly string _reason;
        private float? _zoom = null;
        private ZoomType _zoomType = ZoomType.CenterInside;
        private double? _parallelOffset = null;
        private double? _horizontalOffset = null;
        private double? _verticalOffset = null;
        private double? _page = null;
        private bool _disableAnimation = true;

        private ScrollManager(ReaderView reader, string reason)
        {
            _reader = new WeakReference<ReaderView>(reader);
            _reason = reason;
        }

        public static ScrollManager BeginTransaction(ReaderView reader, string reason)
        {
            return new ScrollManager(reader, reason);
        }

        protected override ScrollResult CommitImpl()
        {
            if (!_reader.TryGetTarget(out ReaderView? reader))
            {
                return ScrollResult.Failed;
            }

            if (!reader._isLoaded)
            {
                return ScrollResult.Failed;
            }

            ScrollResult result;
            if (_parallelOffset.HasValue)
            {
                result = reader.SetScrollViewer1(_zoom, _parallelOffset, _disableAnimation, _reason);
            }
            else if (_page.HasValue)
            {
                result = reader.SetScrollViewer2(_reason, zoom: _zoom, page: _page, disableAnimation: _disableAnimation);
            }
            else
            {
                result = reader.SetScrollViewer3(_zoom, _zoomType, _horizontalOffset, _verticalOffset, _disableAnimation, _reason);
            }

            return result;
        }

        public ScrollManager CopyFrom(ReaderView view)
        {
            _page = view.CurrentPage;
            OnSetOffset();
            return this;
        }

        public ScrollManager Zoom(float? zoom, ZoomType zoomType = ZoomType.CenterInside)
        {
            _zoom = zoom;
            _zoomType = zoomType;
            return this;
        }

        public ScrollManager ParallelOffset(double? parallel_offset)
        {
            _parallelOffset = parallel_offset;
            OnSetOffset();
            return this;
        }

        public ScrollManager HorizontalOffset(double? horizontal_offset)
        {
            _horizontalOffset = horizontal_offset;
            OnSetOffset();
            return this;
        }

        public ScrollManager VerticalOffset(double? vertical_offset)
        {
            _verticalOffset = vertical_offset;
            OnSetOffset();
            return this;
        }

        public ScrollManager Page(double? page)
        {
            _page = page;
            OnSetOffset();
            return this;
        }

        public ScrollManager EnableAnimation()
        {
            _disableAnimation = false;
            return this;
        }

        private void OnSetOffset()
        {
            int checksum = 0;

            if (_page.HasValue)
            {
                checksum++;
            }

            if (_parallelOffset.HasValue)
            {
                checksum++;
            }

            if (_horizontalOffset.HasValue || _verticalOffset.HasValue)
            {
                checksum++;
            }

            if (checksum > 1)
            {
                throw new Exception("Cannot set offset twice.");
            }
        }
    }

    private class ImageDataModel
    {
        public required IImageSource ImageSource { get; set; }
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

    public enum ReaderState
    {
        Idle,
        Ready,
        Loading,
        Error,
    }

    public enum ScrollResult
    {
        Success = 0,
        Failed = 1,
        TooClose = 2,
    }

    public enum ZoomType
    {
        CenterInside,
        CenterCrop,
    }

    private class ScrollRequest
    {
        // Zoom
        public float? zoom = null;
        public ZoomType zoomType = ZoomType.CenterInside;
        public double? page = null;

        // Offset
        public double? horizontalOffset = null;
        public double? verticalOffset = null;

        // Animation
        public bool disableAnimation = false;

        // Options
        public bool ignoreTooClose = false;
    }

    private class ScrollContext
    {
        public float? ZoomPercentage = null;
        public float? ZoomFactor = null;
        public double? HorizontalOffset = null;
        public double? VerticalOffset = null;
        public bool DisableAnimation = false;
    }

    private class PengingImageItem
    {
        public required int Index;
        public required int OriginalWidth;
        public required int OriginalHeight;
        public required IImageSource Source;
    }
}
