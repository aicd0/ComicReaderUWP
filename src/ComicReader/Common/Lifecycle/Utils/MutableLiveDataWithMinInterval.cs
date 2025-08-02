// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using ComicReader.Common.Utils;

using Microsoft.UI.Xaml;

namespace ComicReader.Common.Lifecycle.Utils;

internal class MutableLiveDataWithMinInterval<T>(IMutableLiveData<T> liveData, long minInterval) : IMutableLiveData<T>
{
    private readonly IMutableLiveData<T> _liveData = liveData;
    private readonly long _minInterval = minInterval;

    void ILiveDataNoType.Clear()
    {
        _liveData.Clear();
    }

    void IMutableLiveData<T>.Emit(T value)
    {
        _liveData.Emit(value);
    }

    T? ILiveData<T>.GetValue()
    {
        return _liveData.GetValue();
    }

    void ILiveData<T>.Observe(FrameworkElement owner, IObserver<T> observer)
    {
        _liveData.Observe(owner, new ObserverWrapper<T>(observer, _minInterval));
    }

    void ILiveData<T>.ObserveSticky(FrameworkElement owner, IObserver<T> observer)
    {
        _liveData.ObserveSticky(owner, new ObserverWrapper<T>(observer, _minInterval));
    }

    private class ObserverWrapper<U>(IObserver<U> observer, long minInterval) : IObserver<U>
    {
        private long _lastChangedTime = 0L;
        private U? _lastValue = default;
        private bool _notifyScheduled = false;

        public void OnChanged(U value)
        {
            _lastValue = value;
            if (_notifyScheduled)
            {
                return;
            }

            long currentTime = GetTick();
            long timeElapsed = currentTime - _lastChangedTime;
            if (timeElapsed >= minInterval)
            {
                _lastChangedTime = currentTime;
                observer.OnChanged(value);
                return;
            }

            _notifyScheduled = true;
            CoroutineUtils.Start(async () =>
            {
                try
                {
                    await Task.Delay((int)(minInterval - timeElapsed));
                    _lastChangedTime = GetTick();
                    observer.OnChanged(_lastValue);
                }
                finally
                {
                    _notifyScheduled = false;
                }
            });
        }

        private static long GetTick()
        {
            return Environment.TickCount64;
        }
    }
}
