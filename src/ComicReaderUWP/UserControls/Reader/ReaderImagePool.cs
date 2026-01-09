// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Threading;

using Microsoft.UI.Xaml.Media.Imaging;

namespace ComicReaderUWP.UserControls.Reader;

internal class ReaderImagePool
{
    private readonly CancellationSession _session = new();
    private readonly ITaskDispatcher _dispatcher;
    private readonly OrderedDictionary _entries = [];

    private bool _flushing = false;
    private bool _flushingInvalidated = false;

    public delegate void IRequestCallback(BitmapImage image);

    public ReaderImagePool(ITaskDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void Cancel()
    {
        _session.Next();

        List<string> keys = [];
        foreach (string uri in _entries.Keys)
        {
            keys.Add(uri);
        }

        HashSet<IRequestCallback> callbacks = [];
        foreach (string uri in keys)
        {
            var entry = (ImageEntry)_entries[uri];
            switch (entry.State)
            {
                case ImageEntryState.Requesting:
                case ImageEntryState.Pending:
                    foreach (IRequestCallback callback in entry.Callbacks)
                    {
                        callbacks.Add(callback);
                    }
                    entry.Callbacks.Clear();
                    _entries.Remove(uri);
                    Log($"cancelled {uri}");
                    break;
                case ImageEntryState.Recycled:
                    break;
                default:
                    break;
            }
        }

        foreach (IRequestCallback callback in callbacks)
        {
            callback(null);
        }
    }

    public void RequestImage(IImageSource source, IRequestCallback callback)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));
        ArgumentNullException.ThrowIfNull(callback, nameof(callback));

        string uri = source.GetUri();
        if (uri == null || uri.Length == 0)
        {
            Logger.AssertNotReachHere("D7744CE87FA274AB");
            return;
        }

        Log($"event/request {uri}");
        ImageEntry entry;
        if (_entries.Contains(uri))
        {
            entry = (ImageEntry)_entries[uri];
        }
        else
        {
            entry = new ImageEntry
            {
                State = ImageEntryState.Pending,
                Source = source,
            };
            _entries.Add(uri, entry);
        }

        entry.Callbacks.Add(callback);
    }

    public void CancelRequest(IRequestCallback callback)
    {
        ArgumentNullException.ThrowIfNull(callback, nameof(callback));

        foreach (ImageEntry entry in _entries.Values)
        {
            entry.Callbacks.Remove(callback);
        }
    }

    public void RecycleImage(IImageSource source, BitmapImage image)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));
        ArgumentNullException.ThrowIfNull(image, nameof(image));

        string uri = source.GetUri();
        if (uri == null || uri.Length == 0)
        {
            Logger.AssertNotReachHere("02760A5DB2BA42C4");
            return;
        }

        Log($"event/recycle {uri}");
        ImageEntry entry;
        if (_entries.Contains(uri))
        {
            entry = (ImageEntry)_entries[uri];
        }
        else
        {
            entry = new ImageEntry();
            _entries.Add(uri, entry);
        }

        entry.State = ImageEntryState.Recycled;
        entry.Source = source;
        entry.Image = image;
    }

    public void FlushRequests()
    {
        if (_flushing)
        {
            _flushingInvalidated = true;
            return;
        }

        Log($"event/flush start");
        _flushing = true;
        try
        {
            do
            {
                _flushingInvalidated = false;
                FlushInternal();
            } while (_flushingInvalidated);
        }
        finally
        {
            _flushing = false;
            Log($"event/flush end");
        }
    }

    private void FlushInternal()
    {
        List<string> keys = [];
        foreach (string key in _entries.Keys)
        {
            keys.Add(key);
        }

        foreach (string uri in keys)
        {
            if (!_entries.Contains(uri))
            {
                continue;
            }
            var entry = (ImageEntry)_entries[uri];
            switch (entry.State)
            {
                case ImageEntryState.Requesting:
                    if (entry.Callbacks.Count == 0)
                    {
                        entry.Session.Next();
                        _entries.Remove(uri);
                        Log($"cancelled {uri}");
                    }
                    break;
                case ImageEntryState.Pending:
                    if (entry.Callbacks.Count == 0)
                    {
                        _entries.Remove(uri);
                    }
                    else
                    {
                        var tokens = new List<SimpleImageLoader.Token>
                        {
                            new(entry.Source, new LoadImageResultHandler(this, uri))
                        };
                        var session = new CancellationSession(_session);
                        entry.Session = session;
                        entry.State = ImageEntryState.Requesting;
                        new SimpleImageLoader.Transaction(session.Token, tokens)
                            .SetDispatcher(_dispatcher)
                            .Commit();
                        Log($"submitted {uri}");
                    }
                    break;
                case ImageEntryState.Recycled:
                    if (entry.Callbacks.Count > 0)
                    {
                        Log($"reused {uri}");
                    }
                    else
                    {
                        Log($"removed {uri}");
                    }
                    while (entry.Callbacks.Count > 0)
                    {
                        List<IRequestCallback> callbacks = new(entry.Callbacks);
                        entry.Callbacks.Clear();

                        foreach (IRequestCallback callback in callbacks)
                        {
                            callback(entry.Image);
                        }
                    }
                    _entries.Remove(uri);
                    break;
                default:
                    Logger.AssertNotReachHere("0DA06D20A76086F2");
                    break;
            }
        }
    }

    private static void Log(string message)
    {
        Logger.I("ReaderImagePool", message);
    }

    private class LoadImageResultHandler(ReaderImagePool pool, string uri) : IImageResultHandler
    {
        public void OnSuccess(BitmapImage image)
        {
            Log($"event/loaded {uri}");
            ImageEntry entry;
            if (pool._entries.Contains(uri))
            {
                entry = (ImageEntry)pool._entries[uri];
            }
            else
            {
                Log($"abandoned {uri}");
                return;
            }

            entry.Image = image;
            entry.State = ImageEntryState.Recycled;

            if (entry.Callbacks.Count > 0)
            {
                Log($"loaded {uri}");
            }
            else
            {
                Log($"abandoned {uri}");
            }

            while (entry.Callbacks.Count > 0)
            {
                List<IRequestCallback> callbacks = new(entry.Callbacks);
                entry.Callbacks.Clear();

                foreach (IRequestCallback callback in callbacks)
                {
                    callback(image);
                }
            }

            pool._entries.Remove(uri);
        }
    }

    private enum ImageEntryState
    {
        Requesting,
        Pending,
        Recycled,
    }

    private class ImageEntry
    {
        public ImageEntryState State;
        public IImageSource Source;
        public CancellationSession Session;
        public BitmapImage Image;
        public HashSet<IRequestCallback> Callbacks = [];
    }
}
