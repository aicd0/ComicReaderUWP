// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Imaging;
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
    private const int MAX_BITMAP_SIZE = 16 * 1024 * 1024;
    private const int MAX_CANVAS_DIMENSION = 8192;

    private static readonly ITaskDispatcher _decodeDispatcher = TaskDispatcher.Factory.NewQueue("ReaderViewLoadImageQueue");
    private static readonly ITaskDispatcher _layoutDispatcher = TaskDispatcher.Factory.NewQueue("ReaderImageLayoutWorker");

    private static void Log(string tag, params object?[] values)
    {
        Logger.I(LogTag.N(TAG, tag), string.Join(',', values));
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

    public int HitTest(Windows.Foundation.Point point)
    {
        if (!_resourceRef.TryRef(out InstanceResourceModel? res))
        {
            return -1;
        }

        try
        {
            PointF pointF = new((float)point.X, (float)point.Y);
            lock (res._images)
            {
                for (int i = 0; i < res._images.Count; i++)
                {
                    ImageItem item = res._images[i];
                    lock (item.Lock)
                    {
                        if (item.HitRect.Contains(pointF))
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

        if (!_resourceRef.TryRef(out InstanceResourceModel? res))
        {
            Logger.F(TAG, "SetImage failed: Container is already disposed");
            return;
        }

        Log("SetImage", $"name={Name}-{index}, uri={source?.Source.Uri}");
        ImageItem item;
        try
        {
            bool needDraw = false;
            lock (res._images)
            {
                while (index >= res._images.Count)
                {
                    needDraw = true;
                    res._images.Add(new(res._images.Count));
                }

                item = res._images[index];
            }

            lock (item.Lock)
            {
                if (item.Source != source || item.FrameSize.Width != frameWidth || item.FrameSize.Height != frameHeight)
                {
                    needDraw = true;
                }

                if (!needDraw)
                {
                    return;
                }

                item.Source = source;
                item.FrameSize = new(frameWidth, frameHeight);
                item.SupportVector = false;
                item.ClearPrevious = true;
            }
        }
        finally
        {
            _resourceRef.Unref();
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
        if (Interlocked.Exchange(ref item.InDecodeQueue, 1) == 1)
        {
            return;
        }

        _decodeDispatcher.SubmitAsync(async () =>
        {
            Volatile.Write(ref item.InDecodeQueue, 0);

            if (!_resourceRef.TryRef(out InstanceResourceModel? res))
            {
                return;
            }

            try
            {
                await PerformDecode(item);
            }
            finally
            {
                _resourceRef.Unref();
            }
        });
    }

    private async Task PerformDecode(ImageItem item)
    {
        Log("Decode", $"name={Name}-{item.Index}, uri={item.Source?.Source.Uri}");
        ReaderImageSource? source;
        bool clearPrevious;
        SizeF frameSize;
        lock (item.Lock)
        {
            source = item.Source;
            clearPrevious = item.ClearPrevious;
            item.ClearPrevious = false;
            frameSize = item.FrameSize;
        }

        if (clearPrevious)
        {
            bool needDraw;
            lock (item.Lock)
            {
                needDraw = item.BitmapRef is not null;
                item.BitmapRef?.Unref();
                item.BitmapRef = null;
            }

            if (needDraw)
            {
                PostLayoutTask();
            }
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

            ImageMeta? meta = await ImageLoader.GetImageMeta(source.Source);
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
                PerformLayout(res, version);
            }
            finally
            {
                _resourceRef.Unref();
            }
        });
    }

    private void PerformLayout(InstanceResourceModel res, int version)
    {
        Log("Layout", $"name={Name}, v={version}");
        DrawingItem?[] items;
        SizeF[] frameSizes;
        lock (res._images)
        {
            items = new DrawingItem[res._images.Count];
            frameSizes = new SizeF[res._images.Count];
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

        SizeF mergedFrameSize = new();
        float pixelRatio = 0;
        for (int i = 0; i < items.Length; i++)
        {
            DrawingItem? item = items[i];
            SizeF frameSize = frameSizes[i];

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

            float currentPixelRatio = Math.Max(
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
                    Log("Layout", $"Clear (name={Name}, v={version})");
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

        SizeF canvasSize;
        {
            float accParallelLength = 0;
            float maxPerpendicularLength = 0;
            for (int i = 0; i < items.Length; i++)
            {
                DrawingItem? item = items[i];
                if (item is null && !_placeholderMode)
                {
                    continue;
                }

                SizeF frameSize = frameSizes[i];
                float frameParallelLength = _isHorizontal ? frameSize.Width : frameSize.Height;
                float rectParallelLength = frameParallelLength * pixelRatio;
                accParallelLength += rectParallelLength;

                if (item is not null)
                {
                    float imageParallelLength = _isHorizontal ? item.ImageWidth : item.ImageHeight;
                    float imagePerpendicularLength = _isHorizontal ? item.ImageHeight : item.ImageWidth;
                    float rectPerpendicularLength = rectParallelLength * imagePerpendicularLength / imageParallelLength;
                    maxPerpendicularLength = Math.Max(maxPerpendicularLength, rectPerpendicularLength);
                }
            }

            int maxDimension = MAX_CANVAS_DIMENSION;
            float scaleRatio = Math.Min(1, maxDimension / Math.Max(accParallelLength, maxPerpendicularLength));
            accParallelLength *= scaleRatio;
            maxPerpendicularLength *= scaleRatio;

            accParallelLength = 0;
            float roundedAccParallelLength = 0;
            for (int i = 0; i < items.Length; i++)
            {
                DrawingItem? item = items[i];
                if (item is null && !_placeholderMode)
                {
                    continue;
                }

                SizeF frameSize = frameSizes[i];
                float frameParallelLength = _isHorizontal ? frameSize.Width : frameSize.Height;
                float rectParallelLength = frameParallelLength * pixelRatio * scaleRatio;

                // Round left and right sides to the nearest pixel to prevent gaps
                RectangleF canvasRect = new()
                {
                    X = roundedAccParallelLength
                };
                accParallelLength += rectParallelLength;
                float newRoundedAccParallelLength = (float)Math.Round(accParallelLength);
                canvasRect.Width = Math.Max(1.0F, newRoundedAccParallelLength - roundedAccParallelLength);
                roundedAccParallelLength = newRoundedAccParallelLength;

                if (item is not null)
                {
                    float rectPerpendicularLength = rectParallelLength * item.ImageHeight / item.ImageWidth;
                    canvasRect.Y = (maxPerpendicularLength - rectPerpendicularLength) * 0.5F;
                    canvasRect.Height = rectPerpendicularLength;
                    item.CanvasRect = _isHorizontal ? canvasRect : new(canvasRect.Y, canvasRect.X, canvasRect.Height, canvasRect.Width);
                }
            }

            roundedAccParallelLength = Math.Max(1F, roundedAccParallelLength);
            maxPerpendicularLength = Math.Max(1F, maxPerpendicularLength);
            SizeF contentSize = _isHorizontal ?
                new(roundedAccParallelLength, maxPerpendicularLength) :
                new(maxPerpendicularLength, roundedAccParallelLength);
            float canvasWidthRatio = contentSize.Width / mergedFrameSize.Width;
            float canvasHeightRatio = contentSize.Height / mergedFrameSize.Height;
            canvasSize = canvasWidthRatio < canvasHeightRatio ?
                new(mergedFrameSize.Width * canvasHeightRatio, contentSize.Height) :
                new(contentSize.Width, mergedFrameSize.Height * canvasWidthRatio);

            lock (res._images)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    RectangleF hitRect = new(0, 0, 0, 0);

                    DrawingItem? item = items[i];
                    if (item is not null)
                    {
                        RectangleF canvasRect = item.CanvasRect;
                        hitRect = new RectangleF(
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
                Log("Composite", $"name={Name}, v={version}");
                PerformComposition(res, items, mergedFrameSize, canvasSize);
            }
            finally
            {
                _resourceRef.Unref();
            }
        });
    }

    private void PerformComposition(InstanceResourceModel res, DrawingItem?[] items, SizeF frameSize, SizeF canvasSize)
    {
        if (res._compositionVisual is null)
        {
            SpriteVisual visual = _compositor.CreateSpriteVisual();
            res._compositionVisual = visual;
            res._rootVisual.Children.InsertAtTop(visual);
        }

        res._compositionVisual.Size = new Vector2(frameSize.Width, frameSize.Height);

        Windows.Foundation.Size surfaceSize = new((int)Math.Ceiling(canvasSize.Width), (int)Math.Ceiling(canvasSize.Height));
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
            Log("Composite", $"Remove group (name={Name}, group={_compositionGroup.Id})");
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
                CanvasRect = item.CanvasRect,
            });
        }

        _compositionGroup = new()
        {
            ResourceRef = res._groupRenderResource,
            Items = compositionItems
        };

        Log("Composite", $"Add group (name={Name},group={_compositionGroup.Id})");
        ReaderImageUpdateScheduler.Instance.AddGroup(_compositionGroup);
    }

    private sealed partial class ImageItem(int index) : IDisposable
    {
        public int InDecodeQueue = 0;

        public int Index { get; } = index;
        public object Lock { get; } = new();
        public ReaderImageSource? Source { get; set; }
        public SizeF FrameSize { get; set; }
        public RectangleF HitRect { get; set; }
        public bool SupportVector { get; set; } = false;
        public bool ClearPrevious { get; set; } = false;

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
        public RectangleF CanvasRect { get; set; }

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
