// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Common.Models.F8;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Misc;

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace ComicReaderUWP.UserControls.Reader.Imaging;

internal partial class ReaderImageCompositor : IDisposable
{
    private const string TAG = nameof(ReaderImageCompositor);
    private const int MAX_BITMAP_SIZE = 32 * 1024 * 1024;
    private const int MAX_CANVAS_SIZE = 32 * 1024 * 1024;
    private const int MAX_CANVAS_DIMENSION = 8 * 1024;

    private static int _nextId = 0;
    private static readonly ITaskDispatcher _decodeDispatcher = TaskDispatcher.Factory.NewQueue("ReaderViewLoadImageQueue");
    private static readonly ITaskDispatcher _layoutDispatcher = TaskDispatcher.Factory.NewQueue("ReaderImageLayoutWorker");

    private static void Log(string tag, params object?[] values)
    {
        Logger.I(LogTag.N(TAG, tag), string.Join(',', values));
    }

    private readonly int _id = Interlocked.Increment(ref _nextId);
    private int _postLayout = 0;
    private int _layoutVersion = 0;

    private readonly CanvasDevice _canvasDevice;
    private readonly Compositor _compositor;
    private readonly RefCounted<InstanceResourceModel> _resourceRef;
    private CompositionGroupModel? _compositionGroup;

    public ReaderImageCompositor(UIElement host)
    {
        _canvasDevice = CanvasDevice.GetSharedDevice();
        _compositor = ElementCompositionPreview.GetElementVisual(host).Compositor;
        CompositionGraphicsDevice graphicsDevice = CanvasComposition.CreateCompositionGraphicsDevice(_compositor, _canvasDevice);
        ContainerVisual rootVisual = _compositor.CreateContainerVisual();
        ElementCompositionPreview.SetElementChildVisual(host, rootVisual);
        _resourceRef = new(new(graphicsDevice, rootVisual));
    }

    public string Name { get; set; } = string.Empty;

    private float _scale = float.PositiveInfinity;
    public float Scale
    {
        get => _scale;
        set
        {
            if (float.IsNaN(value) || value <= 0.0)
            {
                return;
            }

            if (_scale == value)
            {
                return;
            }

            _scale = value;
            InvalidateVectorImages();
            PostLayoutTask();
        }
    }

    private bool _placeholderMode = false;
    public bool PlaceholderMode
    {
        get => _placeholderMode;
        set
        {
            if (_placeholderMode == value)
            {
                return;
            }

            _placeholderMode = value;
            PostLayoutTask();
        }
    }

    private bool _isHorizontal = true;
    public bool IsHorizontal
    {
        get => _isHorizontal;
        set
        {
            if (_isHorizontal == value)
            {
                return;
            }

            _isHorizontal = value;
            PostLayoutTask();
        }
    }

    public void Dispose()
    {
        if (_compositionGroup is not null)
        {
            ReaderImageUpdateScheduler.Instance.RemoveGroup(_compositionGroup);
            _compositionGroup = null;
        }

        _resourceRef.Unref();
    }

    public void Invalidate()
    {
        PostLayoutTask();
    }

    public int HitTest(PointF8 point)
    {
        if (!_resourceRef.TryRef(out InstanceResourceModel? res))
        {
            return -1;
        }

        try
        {
            lock (res._images)
            {
                for (int i = 0; i < res._images.Count; i++)
                {
                    ImageItem item = res._images[i];
                    lock (item.Lock)
                    {
                        if (item.HitRect.Contains(point))
                        {
                            return i;
                        }
                    }
                }
            }

            return -1;
        }
        finally
        {
            _resourceRef.Unref();
        }
    }

    public void SetImage(int index, ReaderImageSource? source, float frameWidth, float frameHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));

        Log("SetImage", $"I={_id},N={Name}:{index},Uri={source?.Source.Uri}");

        bool clearPrevious = false;

        if (!_resourceRef.TryRef(out InstanceResourceModel? res))
        {
            Logger.F(TAG, "SetImage failed: Container is already disposed");
            return;
        }

        ImageItem item;
        try
        {
            lock (res._images)
            {
                while (index >= res._images.Count)
                {
                    res._images.Add(new(res._images.Count));
                }

                item = res._images[index];
            }

            lock (item.Lock)
            {
                if (item.Source == source && item.FrameSize.Width == frameWidth && item.FrameSize.Height == frameHeight)
                {
                    return;
                }

                item.Source = source;
                item.FrameSize = new(frameWidth, frameHeight);
                item.SupportVector = false;
                clearPrevious = item.BitmapRef is not null;
                item.BitmapRef?.Unref();
                item.BitmapRef = null;
            }
        }
        finally
        {
            _resourceRef.Unref();
        }

        if (clearPrevious)
        {
            PostLayoutTask();
        }

        PostDecodeTask(item);
    }

    private void InvalidateVectorImages()
    {
        List<ImageItem> vectorItems = [];

        if (!_resourceRef.TryRef(out InstanceResourceModel? res))
        {
            return;
        }

        try
        {
            lock (res._images)
            {
                foreach (ImageItem item in res._images)
                {
                    lock (item.Lock)
                    {
                        if (!item.SupportVector)
                        {
                            continue;
                        }

                        vectorItems.Add(item);
                    }
                }
            }
        }
        finally
        {
            _resourceRef.Unref();
        }

        foreach (ImageItem item in vectorItems)
        {
            PostDecodeTask(item);
        }
    }

    private void PostDecodeTask(ImageItem item)
    {
        Interlocked.Increment(ref item.PendingDecodeTasks);

        _decodeDispatcher.SubmitAsync(async () =>
        {
            if (Interlocked.Decrement(ref item.PendingDecodeTasks) > 0)
            {
                return;
            }

            if (!_resourceRef.TryRef(out InstanceResourceModel? res))
            {
                return;
            }

            try
            {
                Log("Decode", $"Start: I={_id},N={Name}:{item.Index},Uri={item.Source?.Source.Uri}");
                await PerformDecode(item);
                Log("Decode", $"End: I={_id},N={Name}:{item.Index},Uri={item.Source?.Source.Uri}");
            }
            finally
            {
                _resourceRef.Unref();
            }
        });
    }

    private async Task PerformDecode(ImageItem item)
    {
        ReaderImageSource? source;
        SizeF8 frameSize;
        lock (item.Lock)
        {
            source = item.Source;
            frameSize = item.FrameSize;
        }

        if (source is null)
        {
            return;
        }

        using IImageConnection? connection = await source.Source.Open();
        if (connection is null)
        {
            return;
        }

        AnimatedBitmapModel? newBitmap = null;

        using IVectorImageService? vectorService = connection.OpenVectorService();
        item.SupportVector = vectorService is not null;
        if (vectorService is not null)
        {
            double width = frameSize.Width * _scale;
            double height = frameSize.Height * _scale;
            double resolution = width * height;

            if (resolution < 1E-2)
            {
                Logger.W(TAG, $"Decode failed (Invalid resolution) (name={Name}-{item.Index}, uri={item.Source?.Source.Uri})");
                return;
            }

            const double maxResolution = MAX_BITMAP_SIZE;
            if (resolution > maxResolution)
            {
                double ratio = Math.Sqrt(maxResolution / (frameSize.Width * frameSize.Height));
                width = frameSize.Width * ratio;
                height = frameSize.Height * ratio;
            }

            CanvasBitmap? bitmap = await vectorService.CreateImageCanvasBitmap(_canvasDevice,
                (int)Math.Round(width), (int)Math.Round(height));
            if (bitmap is not null)
            {
                newBitmap = AnimatedBitmapModel.FromCanvasBitmap(bitmap);
            }
        }

        if (newBitmap is null)
        {
            using Stream? stream = await connection.OpenImageStream();
            if (stream is null)
            {
                Logger.W(TAG, $"Decode failed (Cannot open stream) (name={Name}-{item.Index}, uri={item.Source?.Source.Uri})");
                return;
            }

            ImageMeta? meta = await ImageLoader.LoadImageMeta(source.Source, new()
            {
                Priority = ImageLoadingPriority.READER_IMAGE,
            });

            if (meta is not null && meta.FrameCount > 1)
            {
                newBitmap = AnimatedBitmapModel.FromStream(_canvasDevice, stream);
            }

            if (newBitmap is null)
            {
                CanvasBitmap bitmap;
                try
                {
                    bitmap = await CanvasBitmap.LoadAsync(_canvasDevice, stream.AsRandomAccessStream());
                }
                catch (Exception ex)
                {
                    Logger.E(TAG, ex);
                    return;
                }

                newBitmap = AnimatedBitmapModel.FromCanvasBitmap(bitmap);
            }
        }

        if (newBitmap is null)
        {
            Logger.W(TAG, $"Decode failed (Unknown format) (name={Name}-{item.Index}, uri={item.Source?.Source.Uri})");
            return;
        }

        RefCounted<AnimatedBitmapModel>? newBitmapRef = new(newBitmap);
        RefCounted<AnimatedBitmapModel>? oldBitmapRef;
        lock (item.Lock)
        {
            oldBitmapRef = item.BitmapRef;
            item.BitmapRef = newBitmapRef;
        }

        oldBitmapRef?.Unref();
        PostLayoutTask();
    }

    private void PostLayoutTask()
    {
        if (Interlocked.Exchange(ref _postLayout, 1) == 1)
        {
            return;
        }

        _layoutDispatcher.Submit(() =>
        {
            int version = Interlocked.Increment(ref _layoutVersion);
            Interlocked.Exchange(ref _postLayout, 0);

            if (!_resourceRef.TryRef(out InstanceResourceModel? res))
            {
                return;
            }

            try
            {
                Log("Layout", $"Start: I={_id},N={Name},V={version}");
                PerformLayout(res, version);
                Log("Layout", $"End: I={_id},N={Name},V={version}");
            }
            finally
            {
                _resourceRef.Unref();
            }
        });
    }

    private void PerformLayout(InstanceResourceModel res, int version)
    {
        DrawingItem?[] items;
        SizeF8[] frameSizes;
        lock (res._images)
        {
            items = new DrawingItem[res._images.Count];
            frameSizes = new SizeF8[res._images.Count];
            for (int i = 0; i < res._images.Count; i++)
            {
                ImageItem item = res._images[i];
                lock (item.Lock)
                {
                    if (item.BitmapRef is null || item.Source is null)
                    {
                        items[i] = null;
                    }
                    else
                    {
                        items[i] = new DrawingItem
                        {
                            OriginalItem = item,
                            Source = item.Source,
                            BitmapRef = item.BitmapRef,
                            ImageSize = item.BitmapRef.Value.SizeInPixels,
                        };
                    }

                    frameSizes[i] = item.FrameSize;
                }
            }
        }

        SizeF8 mergedFrameSize = new();
        double pixelRatio = 0;
        for (int i = 0; i < items.Length; i++)
        {
            DrawingItem? item = items[i];
            SizeF8 frameSize = frameSizes[i];

            if (item is not null || _placeholderMode)
            {
                if (_isHorizontal)
                {
                    mergedFrameSize.Width += frameSize.Width;
                    mergedFrameSize.Height = Math.Max(mergedFrameSize.Height, frameSize.Height);
                }
                else
                {
                    mergedFrameSize.Width = Math.Max(mergedFrameSize.Width, frameSize.Width);
                    mergedFrameSize.Height += frameSize.Height;
                }
            }

            if (item is null)
            {
                continue;
            }

            if (Math.Min(item.ImageSize.Width, item.ImageSize.Height) < 1 ||
                Math.Min(frameSize.Width, frameSize.Height) < 1E-3)
            {
                items[i] = null;
                continue;
            }

            double currentPixelRatio = Math.Max(
                item.ImageWidth / frameSize.Width,
                item.ImageHeight / frameSize.Height);
            pixelRatio = Math.Max(pixelRatio, currentPixelRatio);
        }

        if (pixelRatio < 1E-3)
        {
            CoroutineUtils.RunInMainThread(() =>
            {
                if (Volatile.Read(ref _layoutVersion) != version)
                {
                    return;
                }

                if (!_resourceRef.TryRef(out InstanceResourceModel? res))
                {
                    return;
                }

                try
                {
                    Log("Composite", $"Clear: I={_id},N={Name},V={version}");
                    res.DisposeCompositionComponents();
                }
                finally
                {
                    _resourceRef.Unref();
                }
            });

            return;
        }

        pixelRatio = Math.Min(_scale, pixelRatio);

        Size canvasSize;
        {
            double accParallelLength = 0;
            double maxPerpendicularLength = 0;
            for (int i = 0; i < items.Length; i++)
            {
                DrawingItem? item = items[i];
                if (item is null && !_placeholderMode)
                {
                    continue;
                }

                SizeF8 frameSize = frameSizes[i];
                double frameParallelLength = _isHorizontal ? frameSize.Width : frameSize.Height;
                double rectParallelLength = frameParallelLength * pixelRatio;
                accParallelLength += rectParallelLength;

                if (item is not null)
                {
                    double imageParallelLength = _isHorizontal ? item.ImageWidth : item.ImageHeight;
                    double imagePerpendicularLength = _isHorizontal ? item.ImageHeight : item.ImageWidth;
                    double rectPerpendicularLength = rectParallelLength * imagePerpendicularLength / imageParallelLength;
                    maxPerpendicularLength = Math.Max(maxPerpendicularLength, rectPerpendicularLength);
                }
            }

            double scaleRatio = 1.0;
            {
                double resolution = accParallelLength * maxPerpendicularLength;
                const double maxResolution = MAX_CANVAS_SIZE;
                if (resolution > maxResolution)
                {
                    scaleRatio = Math.Min(scaleRatio, Math.Sqrt(maxResolution / resolution));
                }

                scaleRatio = Math.Min(scaleRatio, MAX_CANVAS_DIMENSION / Math.Max(accParallelLength, maxPerpendicularLength));
            }

            accParallelLength = Math.Floor(accParallelLength * scaleRatio);
            maxPerpendicularLength = Math.Floor(maxPerpendicularLength * scaleRatio);

            accParallelLength = 0;
            double roundedAccParallelLength = 0;
            for (int i = 0; i < items.Length; i++)
            {
                DrawingItem? item = items[i];
                if (item is null && !_placeholderMode)
                {
                    continue;
                }

                SizeF8 frameSize = frameSizes[i];
                double frameParallelLength = _isHorizontal ? frameSize.Width : frameSize.Height;
                double rectParallelLength = frameParallelLength * pixelRatio * scaleRatio;

                // Round to the nearest pixel in parallel axis to prevent gaps
                RectF8 canvasRect = new()
                {
                    X = roundedAccParallelLength
                };
                accParallelLength += rectParallelLength;
                float nextRoundedAccParallelLength = (float)Math.Round(accParallelLength);
                canvasRect.Width = Math.Max(1.0, nextRoundedAccParallelLength - roundedAccParallelLength);
                roundedAccParallelLength = nextRoundedAccParallelLength;

                if (item is not null)
                {
                    // Stretch to nearest pixel in perpendicular axis to prevent gaps
                    double rectPerpendicularLength = Math.Min(rectParallelLength * item.ImageHeight / item.ImageWidth, maxPerpendicularLength);
                    double verticalPadding = (maxPerpendicularLength - rectPerpendicularLength) * 0.5;
                    canvasRect.Y = Math.Floor(verticalPadding);
                    canvasRect.Height = Math.Ceiling(maxPerpendicularLength - verticalPadding) - canvasRect.Y;
                    item.CanvasRect = _isHorizontal ?
                        new((int)canvasRect.X, (int)canvasRect.Y, (int)canvasRect.Width, (int)canvasRect.Height) :
                        new((int)canvasRect.Y, (int)canvasRect.X, (int)canvasRect.Height, (int)canvasRect.Width);
                }
            }

            roundedAccParallelLength = Math.Max(1.0, roundedAccParallelLength);
            maxPerpendicularLength = Math.Max(1.0, maxPerpendicularLength);
            SizeF8 contentSize = _isHorizontal ?
                new(roundedAccParallelLength, maxPerpendicularLength) :
                new(maxPerpendicularLength, roundedAccParallelLength);
            canvasSize = new((int)contentSize.Width, (int)contentSize.Height);

            double canvasWidthRatio = contentSize.Width / mergedFrameSize.Width;
            double canvasHeightRatio = contentSize.Height / mergedFrameSize.Height;
            lock (res._images)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    RectF8 hitRect = new(0, 0, 0, 0);

                    DrawingItem? item = items[i];
                    if (item is not null)
                    {
                        Rectangle canvasRect = item.CanvasRect;
                        hitRect = new RectF8(
                            canvasRect.X / canvasWidthRatio,
                            canvasRect.Y / canvasHeightRatio,
                            canvasRect.Width / canvasWidthRatio,
                            canvasRect.Height / canvasHeightRatio);
                    }

                    res._images[i].HitRect = hitRect;
                }
            }
        }

        CoroutineUtils.RunInMainThread(() =>
        {
            if (Volatile.Read(ref _layoutVersion) != version)
            {
                return;
            }

            if (!_resourceRef.TryRef(out InstanceResourceModel? res))
            {
                return;
            }

            try
            {
                Log("Composite", $"Start: I={_id},N={Name},V={version},Frame={mergedFrameSize},Canvas={canvasSize}");
                PerformComposition(res, version, items, mergedFrameSize, canvasSize);
            }
            finally
            {
                _resourceRef.Unref();
            }
        });
    }

    private void PerformComposition(InstanceResourceModel res, int version, DrawingItem?[] items, SizeF8 frameSize, Size canvasSize)
    {
        if (res._compositionVisual is null)
        {
            SpriteVisual visual = _compositor.CreateSpriteVisual();
            res._compositionVisual = visual;
            res._rootVisual.Children.InsertAtTop(visual);
        }

        res._compositionVisual.Size = (Vector2)frameSize;

        Windows.Foundation.Size surfaceSize = new(canvasSize.Width, canvasSize.Height);
        if (res._groupRenderResource is not null && res._groupRenderResource.Value.Surface.Size != surfaceSize)
        {
            res._groupRenderResource?.Unref();
            res._groupRenderResource = null;
        }

        if (res._groupRenderResource is null)
        {
            CompositionDrawingSurface surface = res._graphicsDevice.CreateDrawingSurface(
                surfaceSize,
                Microsoft.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                Microsoft.Graphics.DirectX.DirectXAlphaMode.Premultiplied);
            CompositionBrush brush = _compositor.CreateSurfaceBrush(surface);
            var offscreenCanvas = new CanvasRenderTarget(
                _canvasDevice,
                (float)surfaceSize.Width,
                (float)surfaceSize.Height,
                96);

            CompositionGroupRenderResource groupRenderResource = new()
            {
                Brush = brush,
                Surface = surface,
                OffscreenCanvas = offscreenCanvas,
            };

            res._groupRenderResource = new(groupRenderResource);
            res._compositionVisual.Brush = brush;
        }

        if (_compositionGroup is not null)
        {
            Log("Composite", $"RemoveGroup: I={_id},N={Name},G={_compositionGroup.Id}");
            ReaderImageUpdateScheduler.Instance.RemoveGroup(_compositionGroup);
            _compositionGroup = null;
        }

        List<CompositionItemModel> compositionItems = [];
        for (int i = 0; i < items.Length; i++)
        {
            DrawingItem? item = items[i];
            if (item is null)
            {
                continue;
            }

            compositionItems.Add(new()
            {
                BitmapRef = item.BitmapRef,
                Source = item.Source,
                CanvasRect = (RectangleF)item.CanvasRect,
            });
        }

        _compositionGroup = new()
        {
            ResourceRef = res._groupRenderResource,
            Items = compositionItems
        };

        Log("Composite", $"AddGroup: I={_id},N={Name},G={_compositionGroup.Id}");
        ReaderImageUpdateScheduler.Instance.AddGroup(_compositionGroup);
    }

    private sealed partial class ImageItem(int index) : IDisposable
    {
        public int PendingDecodeTasks = 0;

        public int Index { get; } = index;
        public object Lock { get; } = new();
        public ReaderImageSource? Source { get; set; }
        public SizeF8 FrameSize { get; set; }
        public RectF8 HitRect { get; set; }
        public bool SupportVector { get; set; } = false;

        public RefCounted<AnimatedBitmapModel>? BitmapRef { get; set; }

        public void Dispose()
        {
            Source = null;
            BitmapRef?.Unref();
            BitmapRef = null;
        }
    }

    private sealed class DrawingItem
    {
        public required ImageItem OriginalItem { get; init; }
        public required ReaderImageSource Source { get; init; }
        public required RefCounted<AnimatedBitmapModel> BitmapRef { get; init; }
        public required Size ImageSize { get; init; }
        public Rectangle CanvasRect { get; set; }

        public int ImageWidth => Source.Settings.Rotation switch
        {
            ImageRotationEnum.Rotate90 or ImageRotationEnum.Rotate270 => ImageSize.Height,
            _ => ImageSize.Width,
        };

        public int ImageHeight => Source.Settings.Rotation switch
        {
            ImageRotationEnum.Rotate90 or ImageRotationEnum.Rotate270 => ImageSize.Width,
            _ => ImageSize.Height,
        };
    }

    private sealed partial class InstanceResourceModel(CompositionGraphicsDevice graphicsDevice, ContainerVisual rootVisual) : IDisposable
    {
        public readonly CompositionGraphicsDevice _graphicsDevice = graphicsDevice;
        public readonly ContainerVisual _rootVisual = rootVisual;
        public readonly List<ImageItem> _images = [];

        public RefCounted<CompositionGroupRenderResource>? _groupRenderResource;
        public SpriteVisual? _compositionVisual;

        public void Dispose()
        {
            foreach (ImageItem item in _images)
            {
                item.Dispose();
            }

            CoroutineUtils.RunInMainThread(() =>
            {
                DisposeCompositionComponents();
                _rootVisual.Dispose();
                _graphicsDevice.Dispose();
            });
        }

        public void DisposeCompositionComponents()
        {
            try
            {
                _rootVisual.Children.RemoveAll();
            }
            catch (ObjectDisposedException)
            {
                // _rootVisual could be disposed externally
            }

            _groupRenderResource?.Unref();
            _groupRenderResource = null;
            _compositionVisual?.Dispose();
            _compositionVisual = null;
        }
    }
}
