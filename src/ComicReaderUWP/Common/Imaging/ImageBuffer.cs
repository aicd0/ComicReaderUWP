// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;

namespace ComicReaderUWP.Common.Imaging;

internal sealed partial class ImageBuffer : IDisposable
{
    private int _disposed = 0;
    private readonly int _size;
    private readonly byte[] _data;

    public int Width { get; }

    public int Height { get; }

    public int BytesPerPixel { get; }

    public Span<byte> Span => _data.AsSpan(0, _size);

    public Windows.Storage.Streams.IBuffer Buffer => _data.AsBuffer(0, _size);

    public ImageBuffer(int width, int height, int bytesPerPixel)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytesPerPixel);

        int size = checked(width * height * bytesPerPixel);
        _size = size;
        _data = ArrayPool<byte>.Shared.Rent(size);
        Width = width;
        Height = height;
        BytesPerPixel = bytesPerPixel;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        ArrayPool<byte>.Shared.Return(_data);
    }
}
