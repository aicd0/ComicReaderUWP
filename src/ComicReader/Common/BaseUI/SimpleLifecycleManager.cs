// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Lifecycle;

namespace ComicReader.Common.BaseUI;

internal class SimpleLifecycleManager
{
    private LogTag? _tag = null;
    private ILifecycleHandler? _lifecycleHandler;
    private readonly SimpleLifecycle _lifecycle = new();
    private readonly PreLifecycleObserver _preLifecycleObserver;
    private readonly PostLifecycleObserver _postLifecycleObserver;

    public SimpleLifecycleManager()
    {
        _preLifecycleObserver = new(this);
        _postLifecycleObserver = new(this);

        _lifecycle.SetPreLifecycleObserver(_preLifecycleObserver);
        _lifecycle.AddObserver(_preLifecycleObserver);
        _lifecycle.SetPostLifecycleObserver(_postLifecycleObserver);
        _lifecycle.AddObserver(_postLifecycleObserver);
    }

    public void Initialize(string name, ILifecycleHandler handler)
    {
        _tag = LogTag.N("PageLifecycle", name);
        _lifecycleHandler = handler;
    }

    public ILifecycle GetLifecycle()
    {
        return _lifecycle;
    }

    public void SetState(ILifecycle.State state)
    {
        _lifecycle.SetState(state);
    }

    private void OnLifecycleEvent(ILifecycle.State fromState, ILifecycle.State toState, bool pre)
    {
        bool goUp = toState > fromState;
        if (goUp)
        {
            switch (toState)
            {
                case ILifecycle.State.Started:
                    OnLifecycleStart(pre);
                    break;
                case ILifecycle.State.Resumed:
                    OnLifecycleResume(pre);
                    break;
                default:
                    throw new ArgumentException($"Invalid transition {fromState} -> {toState}.");
            }
        }
        else
        {
            switch (toState)
            {
                case ILifecycle.State.Stopped:
                    OnLifecycleStop(pre);
                    break;
                case ILifecycle.State.Started:
                    OnLifecyclePause(pre);
                    break;
                default:
                    throw new ArgumentException($"Invalid transition {fromState} -> {toState}.");
            }
        }
    }

    private void OnLifecycleStart(bool pre)
    {
        if (pre)
        {
            LogLifecycleEvent("PreStart");
            if (_lifecycleHandler is not null)
            {
                DebugUtils.TrackError(_lifecycleHandler.PreStart);
            }
        }
        else
        {
            LogLifecycleEvent("PostStart");
            if (_lifecycleHandler is not null)
            {
                DebugUtils.TrackError(_lifecycleHandler.PostStart);
            }
        }
    }

    private void OnLifecycleResume(bool pre)
    {
        if (pre)
        {
            LogLifecycleEvent("PreResume");
            if (_lifecycleHandler is not null)
            {
                DebugUtils.TrackError(_lifecycleHandler.PreResume);
            }
        }
        else
        {
            LogLifecycleEvent("PostResume");
            if (_lifecycleHandler is not null)
            {
                DebugUtils.TrackError(_lifecycleHandler.PostResume);
            }
        }
    }

    private void OnLifecyclePause(bool pre)
    {
        if (pre)
        {
            LogLifecycleEvent("PrePause");
            if (_lifecycleHandler is not null)
            {
                DebugUtils.TrackError(_lifecycleHandler.PrePause);
            }
        }
        else
        {
            LogLifecycleEvent("PostPause");
            if (_lifecycleHandler is not null)
            {
                DebugUtils.TrackError(_lifecycleHandler.PostPause);
            }
        }
    }

    private void OnLifecycleStop(bool pre)
    {
        if (pre)
        {
            LogLifecycleEvent("PreStop");
            if (_lifecycleHandler is not null)
            {
                DebugUtils.TrackError(_lifecycleHandler.PreStop);
            }
        }
        else
        {
            _lifecycle.RemoveObserver(_preLifecycleObserver);
            _lifecycle.SetPreLifecycleObserver(null);
            _lifecycle.RemoveObserver(_postLifecycleObserver);
            _lifecycle.SetPostLifecycleObserver(null);

            LogLifecycleEvent("PostStop");
            if (_lifecycleHandler is not null)
            {
                DebugUtils.TrackError(_lifecycleHandler.PostStop);
            }
        }
    }

    private void LogLifecycleEvent(string eventName)
    {
        if (_tag is not null)
        {
            Logger.I(_tag, eventName);
        }
    }

    private class PreLifecycleObserver(SimpleLifecycleManager manager) : ILifecycleObserver
    {
        public void OnLifecycleEvent(ILifecycle.State fromState, ILifecycle.State toState)
        {
            manager.OnLifecycleEvent(fromState, toState, pre: true);
        }
    }

    private class PostLifecycleObserver(SimpleLifecycleManager manager) : ILifecycleObserver
    {
        public void OnLifecycleEvent(ILifecycle.State fromState, ILifecycle.State toState)
        {
            manager.OnLifecycleEvent(fromState, toState, pre: false);
        }
    }

    public interface ILifecycleHandler
    {
        void PreStart();

        void PostStart();

        void PreResume();

        void PostResume();

        void PrePause();

        void PostPause();

        void PreStop();

        void PostStop();
    }
}
