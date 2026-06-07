// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using Microsoft.Graphics.Canvas;

using SkiaSharp;

namespace ComicReaderUWP.UserControls.Reader.Imaging;

internal sealed partial class AnimatedBitmapModel : IDisposable
{
    public static AnimatedBitmapModel FromCanvasBitmap(CanvasBitmap bitmap)
    {
        Size size = new((int)bitmap.SizeInPixels.Width, (int)bitmap.SizeInPixels.Height);
        List<Frame> frames = [new()
        {
            Bitmap = bitmap,
            Duration = 1,
        }];
        return new(size, frames);
    }

    public static AnimatedBitmapModel? FromStream(ICanvasResourceCreator canvasDevice, Stream stream)
    {
        using var codec = SKCodec.Create(stream);
        if (codec is null)
        {
            return null;
        }

        SKImageInfo info = codec.Info;
        SKCodecFrameInfo[] frameInfo = codec.FrameInfo;
        Size size = new(info.Width, info.Height);

        List<Frame> frames = [];
        for (int i = 0; i < codec.FrameCount; i++)
        {
            using SKBitmap bmp = new(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            codec.GetPixels(bmp.Info, bmp.GetPixels(), new SKCodecOptions(i));

            int dur = frameInfo[i].Duration;
            if (dur <= 0)
            {
                dur = 100;
            }

            int bytes = bmp.RowBytes * bmp.Height;
            CanvasBitmap canvasBitmap;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bytes);
            try
            {
                IntPtr ptr = bmp.GetPixels();
                Marshal.Copy(ptr, buffer, 0, bytes);
                canvasBitmap = CanvasBitmap.CreateFromBytes(
                    canvasDevice,
                    buffer,
                    info.Width,
                    info.Height,
                    Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    96);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            frames.Add(new()
            {
                Bitmap = canvasBitmap,
                Duration = dur,
            });
        }

        return new(size, frames);
    }

    private readonly Size _size;
    private readonly List<Frame> _frames;
    private readonly long _duration;

    public Size SizeInPixels => _size;
    public int FrameCount => _frames.Count;
    public long Duration => _duration;

    private AnimatedBitmapModel(Size size, IEnumerable<Frame> frames)
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

    public CanvasBitmap GetFrameBitmap(int frameIndex)
    {
        return _frames[frameIndex].Bitmap;
    }

    public long GetFrameStartTime(int frameIndex)
    {
        return _frames[frameIndex].StartTime;
    }

    private partial class Frame : IDisposable
    {
        public required CanvasBitmap Bitmap { get; init; }
        public required long Duration { get; init; }
        public long StartTime { get; set; }

        public void Dispose()
        {
            Bitmap.Dispose();
        }
    }
}
