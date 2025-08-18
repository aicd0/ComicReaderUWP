// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace ComicReader.Common.Lifecycle;

public class SimpleLifecycle : ILifecycle
{
    private ILifecycle.State _currentState = ILifecycle.State.Initialized;
    private readonly Dictionary<ILifecycleObserver, ILifecycle.State> _lifecycleObservers = [];
    private bool _syncing = false;
    private bool _syncInvalidated = false;

    public ILifecycle.State GetState()
    {
        return _currentState;
    }

    public void AddObserver(ILifecycleObserver observer)
    {
        _lifecycleObservers.TryAdd(observer, ILifecycle.State.Initialized);
        SyncState(observer);
    }

    public void RemoveObserver(ILifecycleObserver observer)
    {
        _lifecycleObservers.Remove(observer);
    }

    public void SetState(ILifecycle.State newState)
    {
        if (_currentState == newState)
        {
            return;
        }

        _currentState = newState;
        SyncState(null);
    }

    private void SyncState(ILifecycleObserver? initiator)
    {
        if (_syncing)
        {
            _syncInvalidated = true;
            return;
        }

        _syncing = true;
        try
        {
            do
            {
                _syncInvalidated = false;
                if (initiator != null)
                {
                    SyncStateForObserver(initiator);
                    initiator = null;
                }
                else
                {
                    List<ILifecycleObserver> snapshot = [.. _lifecycleObservers.Keys];
                    foreach (ILifecycleObserver observer in snapshot)
                    {
                        SyncStateForObserver(observer);
                        if (_syncInvalidated)
                        {
                            break;
                        }
                    }
                }
            } while (_syncInvalidated);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SyncStateForObserver(ILifecycleObserver observer)
    {
        if (!_lifecycleObservers.TryGetValue(observer, out ILifecycle.State observerState))
        {
            return;
        }

        while (observerState != _currentState)
        {
            ILifecycle.State nextState = GetNextState(observerState, _currentState);
            _lifecycleObservers[observer] = nextState;
            observer.OnLifecycleEvent(observerState, nextState);
            observerState = nextState;

            if (_syncInvalidated)
            {
                break;
            }
        }
    }

    private static ILifecycle.State GetNextState(ILifecycle.State from, ILifecycle.State to)
    {
        if (from == to)
        {
            throw new System.InvalidOperationException("from and to must be different");
        }

        return from switch
        {
            ILifecycle.State.Initialized => to switch
            {
                ILifecycle.State.Started => ILifecycle.State.Started,
                ILifecycle.State.Resumed => ILifecycle.State.Started,
                ILifecycle.State.Stopped => ILifecycle.State.Stopped,
                _ => throw new System.InvalidOperationException("Unknown state: " + to),
            },
            ILifecycle.State.Started => to switch
            {
                ILifecycle.State.Initialized => throw new System.InvalidOperationException("Cannot transition from Started to Initialized"),
                ILifecycle.State.Resumed => ILifecycle.State.Resumed,
                ILifecycle.State.Stopped => ILifecycle.State.Stopped,
                _ => throw new System.InvalidOperationException("Unknown state: " + to),
            },
            ILifecycle.State.Resumed => to switch
            {
                ILifecycle.State.Initialized => throw new System.InvalidOperationException("Cannot transition from Resumed to Initialized"),
                ILifecycle.State.Started => ILifecycle.State.Started,
                ILifecycle.State.Stopped => ILifecycle.State.Stopped,
                _ => throw new System.InvalidOperationException("Unknown state: " + to),
            },
            ILifecycle.State.Stopped => throw new System.InvalidOperationException("Cannot transition from Stopped state"),
            _ => throw new System.InvalidOperationException("Unknown state: " + from),
        };
    }
}
