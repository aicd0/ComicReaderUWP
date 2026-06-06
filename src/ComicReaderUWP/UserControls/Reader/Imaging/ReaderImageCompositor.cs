// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Threading;

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

using Windows.Graphics.Imaging;

namespace ComicReaderUWP.UserControls.Reader.Imaging;

internal partial class ReaderImageCompositor : IDisposable
{
    private const string TAG = nameof(ReaderImageCompositor);
    private const int MAX_BITMAP_SIZE = 16 * 1024 * 1024;
    private const int MAX_CANVAS_DIMENSION = 8192;

    private static readonly ITaskDispatcher _decodeDispatcher = TaskDispatcher.Factory.NewQueue("ReaderViewLoadImageQueue");
    private static readonly ITaskDispatcher _layoutDispatcher = TaskDispatcher.Factory.NewQueue("ReaderImageLayoutWorker");

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

            float fixedValue = (float)(value * DisplayUtils.GetRawPixelPerPixel() * 1.2);
            if (_scale == fixedValue)
            {
                return;
            }

            _scale = fixedValue;
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
    public readonly Compositor _compositor;
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
        _resourceRef.Unref();
    }

    public void Invalidate()
    {
        PostLayoutTask();
    }

    public void SetImage(int index, ReaderImageSource? source, float frameWidth, float frameHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));

        if (!_resourceRef.TryRef(out InstanceResourceModel? res))
        {
            Logger.F(TAG, "SetImage: Container is already disposed");
            return;
        }

        ImageItem item;
        try
        {
            bool needDraw = false;
            lock (res._images)
            {
                while (index >= res._images.Count)
                {
                    needDraw = true;
                    res._images.Add(new());
                }

                item = res._images[index];
            }

            bool supportVector = false;
            if (source is not null)
            {
                using IVectorImageService? vectorService = source.Source.OpenVectorService();
                supportVector = vectorService is not null;
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
                item.SupportVector = supportVector;
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

        _decodeDispatcher.Submit(() =>
        {
            Volatile.Write(ref item.InDecodeQueue, 0);

            if (!_resourceRef.TryRef(out InstanceResourceModel? res))
            {
                return;
            }

            try
            {
                PerformDecode(item);
            }
            finally
            {
                _resourceRef.Unref();
            }
        });
    }

    private void PerformDecode(ImageItem item)
    {
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

        CanvasBitmap? newBitmap;
        using IVectorImageService? vectorService = source.Source.OpenVectorService();
        if (vectorService is not null)
        {
            double width = frameSize.Width * _scale;
            double height = frameSize.Height * _scale;
            double resolution = width * height;

            if (resolution < 1E-2)
            {
                return;
            }

            const double maxResolution = MAX_BITMAP_SIZE;
            if (resolution > maxResolution)
            {
                double ratio = Math.Sqrt(maxResolution / (frameSize.Width * frameSize.Height));
                width = frameSize.Width * ratio;
                height = frameSize.Height * ratio;
            }

            newBitmap = vectorService.CreateImageCanvasBitmap(_canvasDevice,
                (int)Math.Round(width), (int)Math.Round(height));
        }
        else
        {
            using Stream? stream = source.Source.OpenImageStream();
            if (stream is null)
            {
                return;
            }

            try
            {
                newBitmap = CanvasBitmap.LoadAsync(_canvasDevice,
                    stream.AsRandomAccessStream()).AsTask().Result;
            }
            catch (Exception e)
            {
                Logger.E(TAG, e);
                return;
            }
        }

        if (newBitmap is null)
        {
            return;
        }

        RefCounted<CanvasBitmap>? newBitmapRef = new(newBitmap);
        RefCounted<CanvasBitmap>? oldBitmapRef;
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
                            BitmapSize = item.BitmapRef.Value.SizeInPixels,
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

            if (Math.Min(item.BitmapSize.Width, item.BitmapSize.Height) < 1 || Math.Min(frameSize.Width, frameSize.Height) < 1E-3)
            {
                items[i] = null;
                continue;
            }

            float currentPixelRatio = Math.Max(item.ImageWidth / frameSize.Width, item.ImageHeight / frameSize.Height);
            pixelRatio = Math.Max(pixelRatio, currentPixelRatio);
        }

        if (pixelRatio < 1E-3)
        {
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
        Windows.Foundation.Size surfaceSize = new((int)Math.Ceiling(canvasSize.Width), (int)Math.Ceiling(canvasSize.Height));
        if (res._compositionSurfaceRef is not null && res._compositionSurfaceRef.Value.Size != surfaceSize)
        {
            res._compositionBrush?.Dispose();
            res._compositionBrush = null;
            res._compositionSurfaceRef.Unref();
            res._compositionSurfaceRef = null;
        }

        if (res._compositionSurfaceRef is null)
        {
            CompositionDrawingSurface surface = res._graphicsDevice.CreateDrawingSurface(
                surfaceSize,
                Microsoft.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                Microsoft.Graphics.DirectX.DirectXAlphaMode.Premultiplied);
            res._compositionSurfaceRef = new(surface);
            res._compositionBrush?.Dispose();
            res._compositionBrush = _compositor.CreateSurfaceBrush(surface);

            if (res._compositionVisual is null)
            {
                SpriteVisual visual = _compositor.CreateSpriteVisual();
                visual.Size = new Vector2(frameSize.Width, frameSize.Height);
                visual.Brush = res._compositionBrush;
                res._compositionVisual = visual;
                res._rootVisual.Children.InsertAtTop(visual);
            }
        }

        if (_compositionGroup is not null)
        {
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
                ImageSource = item.Source,
                CanvasRect = item.CanvasRect,
            });
        }

        _compositionGroup = new()
        {
            SurfaceRef = res._compositionSurfaceRef,
            Items = compositionItems
        };
        ReaderImageUpdateScheduler.Instance.AddGroup(_compositionGroup);
    }

    private sealed partial class ImageItem : IDisposable
    {
        public int InDecodeQueue = 0;

        public object Lock { get; } = new();
        public ReaderImageSource? Source { get; set; }
        public SizeF FrameSize { get; set; }
        public bool SupportVector { get; set; } = false;
        public bool ClearPrevious { get; set; } = false;

        public RefCounted<CanvasBitmap>? BitmapRef { get; set; }

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
        public required RefCounted<CanvasBitmap> BitmapRef { get; init; }
        public required BitmapSize BitmapSize { get; init; }
        public RectangleF CanvasRect { get; set; }

        public uint ImageWidth => Source.Rotation switch
        {
            ImageRotationEnum.Rotate90 or ImageRotationEnum.Rotate270 => BitmapSize.Height,
            _ => BitmapRef.Value.SizeInPixels.Width,
        };

        public uint ImageHeight => Source.Rotation switch
        {
            ImageRotationEnum.Rotate90 or ImageRotationEnum.Rotate270 => BitmapSize.Width,
            _ => BitmapRef.Value.SizeInPixels.Height,
        };
    }

    private sealed partial class InstanceResourceModel(CompositionGraphicsDevice graphicsDevice, ContainerVisual rootVisual) : IDisposable
    {
        public readonly CompositionGraphicsDevice _graphicsDevice = graphicsDevice;
        public readonly ContainerVisual _rootVisual = rootVisual;
        public readonly List<ImageItem> _images = [];
        public RefCounted<CompositionDrawingSurface>? _compositionSurfaceRef;
        public CompositionSurfaceBrush? _compositionBrush;
        public SpriteVisual? _compositionVisual;

        public void Dispose()
        {
            _rootVisual.Children.RemoveAll();

            _compositionSurfaceRef?.Unref();
            _compositionSurfaceRef = null;
            _compositionBrush?.Dispose();
            _compositionBrush = null;
            _compositionVisual?.Dispose();
            _compositionVisual = null;

            foreach (ImageItem item in _images)
            {
                item.Dispose();
            }

            _images.Clear();

            _rootVisual.Dispose();
            _graphicsDevice.Dispose();
        }
    }
}
