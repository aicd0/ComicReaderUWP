// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;

using ComicReader.Common.Threading;
using ComicReader.SDK.Common.DebugTools;

using Microsoft.UI.Xaml;

namespace ComicReader.Common.Lifecycle;

public class LiveData<T> : ILiveData<T>, ILiveDataNoType
{
    private readonly Dictionary<IObserver<T>, ObserverWrapper> _observers = new();
    private T _value;
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

    public void Observe(FrameworkElement owner, Action<T> observer)
    {
        var wrapper = new Observer<T>(observer);
        Observe(owner, wrapper);
    }

    public void ObserveSticky(FrameworkElement owner, Action<T> observer)
    {
        var wrapper = new Observer<T>(observer);
        ObserveSticky(owner, wrapper);
    }

    public void Observe(FrameworkElement owner, IObserver<T> observer)
    {
        ObserveInternal(owner, observer, false);
    }

    public void ObserveSticky(FrameworkElement owner, IObserver<T> observer)
    {
        ObserveInternal(owner, observer, true);
    }

    public T GetValue()
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
        _ = MainThreadUtils.RunInMainThread(delegate
        {
            _value = value;
            _version++;
            DispatchValue(null, value);
        });
    }

    private void ObserveInternal(FrameworkElement owner, IObserver<T> observer, bool sticky)
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

        if (!owner.IsLoaded)
        {
            return;
        }

        if (_observers.TryGetValue(observer, out ObserverWrapper wrapper))
        {
            if (wrapper.IsSameOwner(owner))
            {
                Logger.AssertNotReachHere("4EC4F8B92CAAE0D0");
            }
            return;
        }

        _observers[observer] = new ObserverWrapper(this, owner, observer);

        if (sticky && _version > 0)
        {
            DispatchValue(observer, _value);
        }
    }

    private void DispatchValue(IObserver<T> initiator, T value)
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
            if (initiator != null)
            {
                ConsiderNotify(initiator, value);
            }
            else
            {
                var snapshot = new List<IObserver<T>>(_observers.Keys);
                foreach (IObserver<T> observer in snapshot)
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

    private void ConsiderNotify(IObserver<T> observer, T value)
    {
        observer.OnChanged(value);
    }

    private class Observer<U> : IObserver<U>
    {
        private readonly Action<U> _action;

        public Observer(Action<U> action)
        {
            _action = action;
        }

        public void OnChanged(U value)
        {
            _action(value);
        }
    }

    private class ObserverWrapper
    {
        private readonly LiveData<T> _liveData;
        private readonly FrameworkElement _owner;
        private readonly IObserver<T> _observer;

        public ObserverWrapper(LiveData<T> liveData, FrameworkElement owner, IObserver<T> observer)
        {
            _liveData = liveData;
            _owner = owner;
            _observer = observer;

            _owner.Unloaded += UnloadedHandler;
        }

        public bool IsSameOwner(FrameworkElement owner)
        {
            return _owner == owner;
        }

        public void Remove()
        {
            _liveData._observers.Remove(_observer);
            _owner.Unloaded -= UnloadedHandler;
        }

        private void UnloadedHandler(object sender, RoutedEventArgs e)
        {
            if (_owner.IsLoaded)
            {
                return;
            }

            Remove();
        }
    }
}
