// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Imaging;

namespace ComicReaderUWP.Data.Models.Comic;

internal sealed partial class ComicConnection(BaseComicConnection connection) : IComicConnection
{
    private bool _disposeRequested = false;
    private readonly Lock _refLock = new();
    private int _refCount = 0;

    private readonly BaseComicConnection _connection = connection;
    private readonly int _imageCount = connection.ImageCount;

    public int ImageCount => _imageCount;

    public void Dispose()
    {
        bool needDispose;
        lock (_refLock)
        {
            _disposeRequested = true;
            needDispose = _refCount == 0;
        }

        if (needDispose)
        {
            DisposeInternal();
        }
    }

    public string GetImageName(int index)
    {
        if (!TryRefConnection(out IComicConnection? connection))
        {
            return string.Empty;
        }

        try
        {
            if (index < 0 || index >= _imageCount)
            {
                return string.Empty;
            }

            return connection.GetImageName(index);
        }
        finally
        {
            UnrefConnection();
        }
    }

    public string GetImagePath(int index)
    {
        if (!TryRefConnection(out IComicConnection? connection))
        {
            return string.Empty;
        }

        try
        {
            if (index < 0 || index >= _imageCount)
            {
                return string.Empty;
            }

            return connection.GetImagePath(index);
        }
        finally
        {
            UnrefConnection();
        }
    }

    public string GetImageCacheKey(int index)
    {
        if (!TryRefConnection(out IComicConnection? connection))
        {
            return string.Empty;
        }

        try
        {
            if (index < 0 || index >= _imageCount)
            {
                return string.Empty;
            }

            return connection.GetImageCacheKey(index);
        }
        finally
        {
            UnrefConnection();
        }
    }

    public string GetImageSignature(int index)
    {
        if (!TryRefConnection(out IComicConnection? connection))
        {
            return string.Empty;
        }

        try
        {
            if (index < 0 || index >= _imageCount)
            {
                return string.Empty;
            }

            return connection.GetImageSignature(index);
        }
        finally
        {
            UnrefConnection();
        }
    }

    public async Task<Stream?> OpenImageStream(int index)
    {
        if (!TryRefConnection(out IComicConnection? connection))
        {
            return null;
        }

        try
        {
            if (index < 0 || index >= _imageCount)
            {
                return null;
            }

            return await connection.OpenImageStream(index);
        }
        finally
        {
            UnrefConnection();
        }
    }

    public IVectorImageService? OpenVectorService(int index)
    {
        if (!TryRefConnection(out IComicConnection? connection))
        {
            return null;
        }

        try
        {
            if (index < 0 || index >= _imageCount)
            {
                return null;
            }

            return connection.OpenVectorService(index);
        }
        finally
        {
            UnrefConnection();
        }
    }

    private bool TryRefConnection([NotNullWhen(true)] out IComicConnection? connection)
    {
        connection = null;

        if (_disposeRequested)
        {
            return false;
        }

        lock (_refLock)
        {
            if (_disposeRequested)
            {
                return false;
            }

            _refCount++;
        }

        connection = _connection;
        return true;
    }

    private void UnrefConnection()
    {
        bool needDispose = false;
        lock (_refLock)
        {
            if (_refCount == 0)
            {
                return;
            }

            _refCount--;

            if (_refCount == 0 && _disposeRequested)
            {
                needDispose = true;
            }
        }

        if (needDispose)
        {
            DisposeInternal();
        }
    }

    private void DisposeInternal()
    {
        _connection.Dispose();
    }
}
