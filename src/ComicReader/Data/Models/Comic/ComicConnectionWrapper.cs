// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using ComicReader.SDK.Common.DebugTools;

using Windows.Storage.Streams;

namespace ComicReader.Data.Models.Comic;

internal sealed partial class ComicConnectionWrapper(IComicConnection connection) : IComicConnection
{
    private const string TAG = nameof(ComicConnectionWrapper);

    private bool _disposed = false;
    private readonly IComicConnection _connection = connection;

    void IDisposable.Dispose()
    {
        if (_disposed)
        {
            Logger.F(TAG, "The connection is already disposed.");
            return;
        }

        _disposed = true;
        _connection.Dispose();
    }

    int IComicConnection.GetImageCount()
    {
        if (_disposed)
        {
            Logger.F(TAG, "The connection is already disposed.");
            return 0;
        }

        return _connection.GetImageCount();
    }

    Task<IRandomAccessStream?> IComicConnection.GetImageStream(int index)
    {
        if (_disposed)
        {
            Logger.F(TAG, "The connection is already disposed.");
            return Task.FromResult<IRandomAccessStream?>(null);
        }

        return _connection.GetImageStream(index);
    }

    public string GetImageName(int index)
    {
        if (_disposed)
        {
            Logger.F(TAG, "The connection is already disposed.");
            return string.Empty;
        }

        return _connection.GetImageName(index);
    }
}
