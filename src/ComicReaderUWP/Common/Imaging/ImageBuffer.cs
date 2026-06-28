// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;

using Microsoft.Graphics.Canvas;

using Windows.Graphics.DirectX;

namespace ComicReaderUWP.Common.Imaging;

internal sealed partial class ImageBuffer : IDisposable
{
    private int _bufferReturned = 0;
    private readonly int _size;
    private readonly DirectXPixelFormat _pixelFormat;
    private readonly byte[] _data;

    public int Width { get; }

    public int Height { get; }

    public byte[] UnsafeBytes => _data;

    public Span<byte> Span => _data.AsSpan(0, _size);

    public Windows.Storage.Streams.IBuffer Buffer => _data.AsBuffer(0, _size);

    public ImageBuffer(int size, int width, int height, DirectXPixelFormat pixelFormat)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        _size = size;
        _pixelFormat = pixelFormat;
        _data = ArrayPool<byte>.Shared.Rent(size);
        Width = width;
        Height = height;
    }

    public ImageBuffer(byte[] data, int width, int height, DirectXPixelFormat pixelFormat)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(data.Length);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Volatile.Write(ref _bufferReturned, 1);
        _size = data.Length;
        _pixelFormat = pixelFormat;
        _data = data;
        Width = width;
        Height = height;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _bufferReturned, 1) == 0)
        {
            ArrayPool<byte>.Shared.Return(_data);
        }
    }

    public CanvasBitmap CreateCanvasBitmap(ICanvasResourceCreator device)
    {
        return CanvasBitmap.CreateFromBytes(device, Buffer, Width, Height, _pixelFormat, 96);
    }
}
