// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReader.SDK.Common.Lifecycle;

namespace ComicReader.Common.BaseUI;

internal class LifecycleAwareAbility : ILifecycleAwareAbility
{
    private PageLifecycleEventHandler? _pageLifecycle;
    private ILifecycle.State _state = ILifecycle.State.Resumed;
    private bool _stopped = false;
    private readonly Dictionary<string, ILifecycle.State> _customStates = [];
    private readonly Dictionary<ILifecycleOwner, LifecycleObserver> _ownerStates = [];

    public void RegisterPageLifecycleHandler(PageLifecycleEventHandler handler)
    {
        _pageLifecycle += handler;
        handler(_state);
    }

    public void UnregisterPageLifecycleHandler(PageLifecycleEventHandler handler)
    {
        _pageLifecycle -= handler;
    }

    public void SetCustomState(string key, ILifecycle.State state)
    {
        _customStates[key] = state;
        UpdateState();
    }

    public void Observe(ILifecycleOwner owner)
    {
        if (_ownerStates.ContainsKey(owner))
        {
            return;
        }

        LifecycleObserver observer = new(this, owner);
        _ownerStates.Add(owner, observer);
        owner.GetLifecycle().AddObserver(observer);
    }

    private void UpdateState()
    {
        ILifecycle.State finalState;
        if (_stopped)
        {
            finalState = ILifecycle.State.Stopped;
        }
        else
        {
            finalState = ILifecycle.State.Resumed;

            foreach (ILifecycle.State state in _customStates.Values)
            {
                finalState = (ILifecycle.State)Math.Min((int)state, (int)finalState);
            }

            List<ILifecycleOwner> owners = [.. _ownerStates.Keys];
            foreach (ILifecycleOwner owner in owners)
            {
                ILifecycle.State state = owner.GetLifecycle().GetState();
                finalState = (ILifecycle.State)Math.Min((int)state, (int)finalState);

                if (state == ILifecycle.State.Stopped)
                {
                    owner.GetLifecycle().RemoveObserver(_ownerStates[owner]);
                    _ownerStates.Remove(owner);
                }
            }

            if (finalState == ILifecycle.State.Stopped)
            {
                _stopped = true;
            }
        }

        if (finalState != _state)
        {
            _state = finalState;
            DispatchPageLifecycleEvent(finalState);
        }
    }

    private void DispatchPageLifecycleEvent(ILifecycle.State state)
    {
        _pageLifecycle?.Invoke(state);
    }

    private class LifecycleObserver(LifecycleAwareAbility ability, ILifecycleOwner owner) : ILifecycleObserver
    {
        public void OnLifecycleEvent(ILifecycle.State fromState, ILifecycle.State toState)
        {
            if (toState == owner.GetLifecycle().GetState())
            {
                ability.UpdateState();
            }
        }
    }
}
