// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;

namespace ComicReaderUWP.Data.Models.Comic;

internal sealed partial class ComicConnectionWrapper(IComicConnection connection) : IComicConnection
{
    private volatile bool _disposed = false;
    private readonly IComicConnection _connection = connection;

    void IDisposable.Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connection.Dispose();
    }

    int IComicConnection.GetImageCount()
    {
        if (_disposed)
        {
            return 0;
        }

        return _connection.GetImageCount();
    }

    public Stream? GetImageStream(int index)
    {
        if (_disposed)
        {
            return null;
        }

        return _connection.GetImageStream(index);
    }

    public string GetImageName(int index)
    {
        if (_disposed)
        {
            return string.Empty;
        }

        return _connection.GetImageName(index);
    }

    string IComicConnection.GetImageCacheKey(int index)
    {
        if (_disposed)
        {
            return string.Empty;
        }

        return _connection.GetImageCacheKey(index);
    }

    string IComicConnection.GetImageSignature(int index)
    {
        if (_disposed)
        {
            return string.Empty;
        }

        return _connection.GetImageSignature(index);
    }
}
