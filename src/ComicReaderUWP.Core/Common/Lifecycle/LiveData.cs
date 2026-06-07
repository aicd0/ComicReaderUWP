// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Utils;

namespace ComicReaderUWP.Core.Common.Lifecycle;

public class LiveData<T> : ILiveData<T>, ILiveDataNoType where T : notnull
{
    private readonly Dictionary<IValueObserver<T>, ObserverWrapper> _observers = [];
    private T? _value;
    private int _version = 0;
    private bool _dispatchingValue = false;
    private bool _dispatchInvalidated = false;
    private bool _clearing = false;

    protected LiveData()
    {
        _value = default;
    }

    protected LiveData(T initialValue)
    {
        _value = initialValue;
        _version = 1;
    }

    public void Observe(ILifecycleOwner owner, IValueObserver<T> observer, ObserveOptions options)
    {
        ObserveInternal(owner, observer, options);
    }

    public void RemoveObserver(IValueObserver<T> observer)
    {
        if (_observers.TryGetValue(observer, out ObserverWrapper? wrapper))
        {
            wrapper.Remove();
        }
    }

    public T? GetValue()
    {
        return _value;
    }

    public void Clear()
    {
        var snapshot = new List<ObserverWrapper>(_observers.Values);
        _clearing = true;
        foreach (ObserverWrapper wrapper in snapshot)
        {
            wrapper.Remove();
        }

        _clearing = false;
    }

    protected void EmitInternal(T value)
    {
        CoroutineUtils.RunInMainThread(delegate
        {
            _value = value;
            _version++;
            DispatchValue(null);
        });
    }

    private void ObserveInternal(ILifecycleOwner owner, IValueObserver<T> observer, ObserveOptions options)
    {
        if (_clearing)
        {
            return;
        }

        if (owner == null || observer == null)
        {
            Logger.AssertNotReachHere("3CC47B4DD23EFA9E");
            return;
        }

        if (_observers.TryGetValue(observer, out ObserverWrapper? wrapper))
        {
            if (wrapper.IsSameOwner(owner))
            {
                Logger.AssertNotReachHere("4EC4F8B92CAAE0D0");
            }

            return;
        }

        ILifecycle.State aliveState;
        ILifecycle.State activeState;
        switch (options.PublishBehavior)
        {
            case LiveDataPublishBehavior.ActiveOnStart:
                aliveState = ILifecycle.State.Started;
                activeState = ILifecycle.State.Started;
                break;
            case LiveDataPublishBehavior.ResumeOnly:
                aliveState = ILifecycle.State.Resumed;
                activeState = ILifecycle.State.Resumed;
                break;
            default:
                aliveState = ILifecycle.State.Started;
                activeState = ILifecycle.State.Resumed;
                break;
        }

        if (owner.GetLifecycle().GetState() < aliveState)
        {
            return;
        }

        ObserverWrapper observerWrapper = new LifecycleObserverWrapper(this, owner, observer, aliveState, activeState);
        if (!options.Sticky)
        {
            observerWrapper.Version = _version;
        }

        _observers[observer] = observerWrapper;
        if (observerWrapper.Version < _version)
        {
            DispatchValue(observerWrapper);
        }
    }

    private void DispatchValue(ObserverWrapper? initiator)
    {
        if (_dispatchingValue)
        {
            _dispatchInvalidated = true;
            return;
        }

        _dispatchingValue = true;
        do
        {
            _dispatchInvalidated = false;
            T value = _value!;
            if (initiator != null)
            {
                ConsiderNotify(initiator, value);
            }
            else
            {
                var snapshot = new List<ObserverWrapper>(_observers.Values);
                foreach (ObserverWrapper observer in snapshot)
                {
                    ConsiderNotify(observer, value);
                    if (_dispatchInvalidated)
                    {
                        break;
                    }
                }
            }
        } while (_dispatchInvalidated);
        _dispatchingValue = false;
    }

    private void ConsiderNotify(ObserverWrapper observer, T value)
    {
        if (observer.Version >= _version || !observer.IsActive())
        {
            return;
        }

        observer.Version = _version;
        observer.Observer.OnChanged(value);
    }

    private abstract class ObserverWrapper(IValueObserver<T> observer)
    {
        public readonly IValueObserver<T> Observer = observer;
        public int Version { get; set; } = 0;

        public abstract bool IsSameOwner(ILifecycleOwner owner);

        public abstract bool IsActive();

        public abstract void Remove();
    }

    private class LifecycleObserverWrapper : ObserverWrapper, ILifecycleObserver
    {
        private readonly LiveData<T> _liveData;
        private readonly ILifecycleOwner _owner;
        private readonly ILifecycle.State _aliveState;
        private readonly ILifecycle.State _activeState;

        public LifecycleObserverWrapper(
            LiveData<T> liveData,
            ILifecycleOwner owner,
            IValueObserver<T> observer,
            ILifecycle.State aliveState,
            ILifecycle.State activeState) : base(observer)
        {
            _liveData = liveData;
            _owner = owner;
            _aliveState = aliveState;
            _activeState = activeState;
            _owner.GetLifecycle().AddObserver(this);
        }

        public override bool IsSameOwner(ILifecycleOwner owner)
        {
            return _owner == owner;
        }

        public override bool IsActive()
        {
            return _owner.GetLifecycle().GetState() >= _activeState;
        }

        public override void Remove()
        {
            _liveData._observers.Remove(Observer);
            _owner.GetLifecycle().RemoveObserver(this);
        }

        void ILifecycleObserver.OnLifecycleEvent(ILifecycle.State fromState, ILifecycle.State toState)
        {
            if (fromState >= _aliveState && toState < _aliveState)
            {
                Remove();
                return;
            }

            if (toState >= _activeState && _liveData._version > Version)
            {
                _liveData.DispatchValue(this);
            }
        }
    }

    private class ForeverObserverWrapper(LiveData<T> liveData, IValueObserver<T> observer) : ObserverWrapper(observer)
    {
        private readonly LiveData<T> _liveData = liveData;

        public override bool IsSameOwner(ILifecycleOwner owner)
        {
            return false;
        }

        public override bool IsActive()
        {
            return true;
        }

        public override void Remove()
        {
            _liveData._observers.Remove(Observer);
        }
    }
}
