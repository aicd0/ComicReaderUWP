// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;

using ComicReaderUWP.Common.Imaging;

namespace ComicReaderUWP.Data.Models.Comic;

internal sealed partial class ComicConnectionWrapper(IComicConnection connection) : IComicConnection
{
    private int _disposed = 0;
    private readonly IComicConnection _connection = connection;
    private readonly int _imageCount = connection.GetImageCount();

    private bool Disposed => Volatile.Read(ref _disposed) == 1;

    void IDisposable.Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _connection.Dispose();
    }

    int IComicConnection.GetImageCount()
    {
        return _imageCount;
    }

    public string GetImageName(int index)
    {
        if (Disposed || index < 0 || index >= _imageCount)
        {
            return string.Empty;
        }

        return _connection.GetImageName(index);
    }

    string IComicConnection.GetImageCacheKey(int index)
    {
        if (Disposed || index < 0 || index >= _imageCount)
        {
            return string.Empty;
        }

        return _connection.GetImageCacheKey(index);
    }

    string IComicConnection.GetImageSignature(int index)
    {
        if (Disposed || index < 0 || index >= _imageCount)
        {
            return string.Empty;
        }

        return _connection.GetImageSignature(index);
    }

    public Stream? OpenImageStream(int index)
    {
        if (Disposed || index < 0 || index >= _imageCount)
        {
            return null;
        }

        return _connection.OpenImageStream(index);
    }

    public IVectorImageService? OpenVectorService(int index)
    {
        if (Disposed || index < 0 || index >= _imageCount)
        {
            return null;
        }

        return _connection.OpenVectorService(index);
    }
}
