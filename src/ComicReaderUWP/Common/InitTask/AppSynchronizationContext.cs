// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;

using ComicReaderUWP.Core.Common.DebugTools;

namespace ComicReaderUWP.Common.InitTask;

internal class AppSynchronizationContext(SynchronizationContext inner) : SynchronizationContext
{
    private readonly SynchronizationContext _inner = inner;

    public override void Send(SendOrPostCallback d, object? state)
    {
        void Wrapped(object? o)
        {
            try
            {
                d(o);
            }
            catch (Exception ex)
            {
                DebugUtils.CaptureFatalError("An unknown error occurred in AppSynchronizationContext#Send#Wrapped.", ex);
                throw;
            }
        }

        try
        {
            _inner.Send(Wrapped, state);
        }
        catch (Exception ex)
        {
            DebugUtils.CaptureFatalError("An unknown error occurred in AppSynchronizationContext#Send.", ex);
            throw;
        }
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        void Wrapped(object? o)
        {
            try
            {
                d(o);
            }
            catch (Exception ex)
            {
                DebugUtils.CaptureFatalError("An unknown error occurred in AppSynchronizationContext#Post#Wrapped.", ex);
                throw;
            }
        }

        try
        {
            _inner.Post(Wrapped, state);
        }
        catch (Exception ex)
        {
            DebugUtils.CaptureFatalError("An unknown error occurred in AppSynchronizationContext#Post.", ex);
            throw;
        }
    }

    public override SynchronizationContext CreateCopy()
    {
        return new AppSynchronizationContext(_inner.CreateCopy());
    }

    public override void OperationStarted()
    {
        _inner.OperationStarted();
    }

    public override void OperationCompleted()
    {
        _inner.OperationCompleted();
    }

    public override int Wait(nint[] waitHandles, bool waitAll, int millisecondsTimeout)
    {
        return _inner.Wait(waitHandles, waitAll, millisecondsTimeout);
    }

    public override string? ToString()
    {
        return _inner.ToString();
    }

    public override bool Equals(object? obj)
    {
        if (obj is not AppSynchronizationContext other)
        {
            return false;
        }

        return _inner.Equals(other._inner);
    }

    public override int GetHashCode()
    {
        return _inner.GetHashCode();
    }
}
