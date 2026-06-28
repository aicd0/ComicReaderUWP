// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Core.Common.DebugTools;

using Microsoft.Graphics.Canvas;

using SkiaSharp;

using Windows.Graphics.Imaging;

namespace ComicReaderUWP.UserControls.Reader.Imaging;

internal sealed partial class ReaderBitmapModel : IReaderBitmapModelForCPU, IReaderBitmapModelForGPU
{
    private const string TAG = nameof(ReaderBitmapModel);

    public static IReaderBitmapModelForCPU FromImageBuffer(ImageBuffer buffer)
    {
        Size size = new(buffer.Width, buffer.Height);
        List<Frame> frames = [new(buffer, 1)];
        return new ReaderBitmapModel(size, frames);
    }

    public static async Task<IReaderBitmapModelForCPU?> FromStreamUsingNativeDecoder(Stream stream)
    {
        BitmapDecoder decoder;
        try
        {
            decoder = await BitmapDecoder.CreateAsync(stream.AsRandomAccessStream());
        }
        catch (Exception ex)
        {
            Logger.E(TAG, ex);
            return null;
        }

        byte[] bytes;
        try
        {
            PixelDataProvider pixelProvider = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                new BitmapTransform(),
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.ColorManageToSRgb);
            bytes = pixelProvider.DetachPixelData();
        }
        catch (Exception ex)
        {
            Logger.E(TAG, ex);
            return null;
        }

        ImageBuffer buffer = new(
            bytes,
            (int)decoder.PixelWidth,
            (int)decoder.PixelHeight,
            Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized);

        Size size = new((int)decoder.PixelWidth, (int)decoder.PixelHeight);
        List<Frame> frames = [new(buffer, 1)];
        return new ReaderBitmapModel(size, frames);
    }

    public static IReaderBitmapModelForCPU? FromStreamUsingSkiaDecoder(Stream stream)
    {
        using var codec = SKCodec.Create(stream);
        if (codec is null)
        {
            return null;
        }

        SKImageInfo info = codec.Info;
        SKCodecFrameInfo[] frameInfo = codec.FrameInfo;

        List<Frame> frames = [];
        for (int i = 0; i < codec.FrameCount; i++)
        {
            using SKBitmap bmp = new(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            SKCodecResult result = codec.GetPixels(bmp.Info, bmp.GetPixels(), new SKCodecOptions(i));
            if (result != SKCodecResult.Success)
            {
                return null;
            }

            int dur = frameInfo[i].Duration;
            if (dur <= 0)
            {
                dur = 100;
            }

            ImageBuffer buffer = new(bmp.ByteCount, bmp.Width, bmp.Height, Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized);
            Marshal.Copy(bmp.GetPixels(), buffer.UnsafeBytes, 0, bmp.ByteCount);

            frames.Add(new(buffer, dur));
        }

        Size size = new(info.Width, info.Height);
        return new ReaderBitmapModel(size, frames);
    }

    private readonly Size _size;
    private readonly List<Frame> _frames;
    private readonly long _duration;

    public Size SizeInPixels => _size;
    public int FrameCount => _frames.Count;
    public long Duration => _duration;

    private ReaderBitmapModel(Size size, IEnumerable<Frame> frames)
    {
        _size = size;
        _frames = [.. frames.Where(f => f.Duration > 0)];
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_frames.Count, nameof(frames));

        long elapsedTime = 0;
        foreach (Frame frame in frames)
        {
            frame.StartTime = elapsedTime;
            elapsedTime += frame.Duration;
        }

        _duration = elapsedTime;
    }

    public void Dispose()
    {
        foreach (Frame frame in _frames)
        {
            frame.Dispose();
        }

        _frames.Clear();
    }

    public IReaderBitmapModelForGPU UploadToGPU(ICanvasResourceCreator device)
    {
        foreach (Frame frame in _frames)
        {
            frame.UploadToGPU(device);
        }

        return this;
    }

    public int GetFrameIndexAtTime(long elapsedMs)
    {
        if (_frames.Count == 1)
        {
            return 0;
        }

        // Map elapsedMs into [0, _duration)
        long t = (_duration == 0) ? 0 : ((elapsedMs % _duration) + _duration) % _duration;

        int lo = 0;
        int hi = _frames.Count - 1;

        // Binary search for the rightmost frame with StartTime <= t
        while (lo <= hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            long start = _frames[mid].StartTime;

            if (start <= t)
            {
                // If this is the last frame or the next frame starts after t, mid is the answer
                if (mid == _frames.Count - 1 || _frames[mid + 1].StartTime > t)
                {
                    return mid;
                }

                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        // Fallback: return first frame
        return 0;
    }

    public long GetFrameStartTime(int frameIndex)
    {
        return _frames[frameIndex].StartTime;
    }

    public CanvasBitmap GetFrameBitmap(int frameIndex)
    {
        return _frames[frameIndex].Bitmap;
    }

    private partial class Frame(ImageBuffer buffer, long duration) : IDisposable
    {
        private ImageBuffer? _buffer = buffer;
        private CanvasBitmap? _bitmap;

        public long Duration { get; } = duration;
        public long StartTime { get; set; }
        public CanvasBitmap Bitmap => _bitmap!;

        public void UploadToGPU(ICanvasResourceCreator device)
        {
            if (_bitmap is null)
            {
                ImageBuffer buffer = _buffer!;
                _bitmap = buffer.CreateCanvasBitmap(device);
            }

            _buffer?.Dispose();
            _buffer = null;
        }

        public void Dispose()
        {
            _buffer?.Dispose();
            _buffer = null;
            _bitmap?.Dispose();
            _bitmap = null;
        }
    }
}
