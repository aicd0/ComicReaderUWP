// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.Lifecycle;

public class SimpleLifecycle : ILifecycle
{
    private ILifecycle.State _currentState = ILifecycle.State.Initialized;
    private readonly Dictionary<ILifecycleObserver, ILifecycle.State> _lifecycleObservers = [];
    private ILifecycleObserver? _preLifecycleObserver = null;
    private ILifecycleObserver? _postLifecycleObserver = null;
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

    public void SetPreLifecycleObserver(ILifecycleObserver? observer)
    {
        _preLifecycleObserver = observer;
    }

    public void SetPostLifecycleObserver(ILifecycleObserver? observer)
    {
        _postLifecycleObserver = observer;
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
                    foreach (ILifecycleObserver observer in GetObserverSnapshot())
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

    private IEnumerable<ILifecycleObserver> GetObserverSnapshot()
    {
        HashSet<ILifecycleObserver> normalObservers = [.. _lifecycleObservers.Keys];
        ILifecycleObserver? preObserver = _preLifecycleObserver;
        ILifecycleObserver? postObserver = _postLifecycleObserver;

        if (preObserver is not null)
        {
            normalObservers.Remove(preObserver);
        }

        if (postObserver is not null)
        {
            normalObservers.Remove(postObserver);
        }

        if (preObserver is not null)
        {
            yield return preObserver;
        }

        foreach (ILifecycleObserver observer in normalObservers)
        {
            yield return observer;
        }

        if (postObserver is not null)
        {
            yield return postObserver;
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
        Exception InvalidTransitionException()
        {
            return new InvalidOperationException($"Invalid state transition: {from} -> {to}.");
        }

        if (from == to)
        {
            throw InvalidTransitionException();
        }

        return from switch
        {

            ILifecycle.State.Initialized => to switch
            {
                ILifecycle.State.Started => ILifecycle.State.Started,
                ILifecycle.State.Resumed => ILifecycle.State.Started,
                ILifecycle.State.Stopped => ILifecycle.State.Stopped,
                _ => throw InvalidTransitionException(),
            },
            ILifecycle.State.Started => to switch
            {
                ILifecycle.State.Resumed => ILifecycle.State.Resumed,
                ILifecycle.State.Stopped => ILifecycle.State.Stopped,
                _ => throw InvalidTransitionException(),
            },
            ILifecycle.State.Resumed => to switch
            {
                ILifecycle.State.Started => ILifecycle.State.Started,
                ILifecycle.State.Stopped => ILifecycle.State.Started,
                _ => throw InvalidTransitionException(),
            },
            ILifecycle.State.Stopped => throw InvalidTransitionException(),
            _ => throw InvalidTransitionException(),
        };
    }
}
