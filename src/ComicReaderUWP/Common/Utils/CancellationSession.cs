// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;

namespace ComicReaderUWP.Common.Utils;

internal sealed class CancellationSession
{
    private readonly Lock _lock = new();
    private readonly List<WeakReference<CancellationSession>> _subSessions = [];
    private CancellableToken _token = new();

    public IToken Token => _token;

    public CancellationSession() : this(null) { }

    public CancellationSession(CancellationSession? baseSession)
    {
        if (baseSession != null)
        {
            lock (baseSession._lock)
            {
                baseSession._subSessions.Add(new WeakReference<CancellationSession>(this));
            }
        }
    }

    public IToken Next()
    {
        lock (_lock)
        {
            _token.Cancel();
            _token = new CancellableToken();

            for (int i = _subSessions.Count - 1; i >= 0; --i)
            {
                WeakReference<CancellationSession> subSessionRef = _subSessions[i];
                if (!subSessionRef.TryGetTarget(out CancellationSession? subSession))
                {
                    _subSessions.RemoveAt(i);
                    continue;
                }

                subSession.Next();
            }

            return _token;
        }
    }

    public interface IToken
    {
        public bool IsCancellationRequested { get; }
    }

    private sealed class CancellableToken : IToken
    {
        private bool _cancelled = false;

        public bool IsCancellationRequested => Volatile.Read(ref _cancelled);

        public void Cancel()
        {
            Volatile.Write(ref _cancelled, true);
        }
    }
}
