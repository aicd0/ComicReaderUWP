// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.SDK.Common.Utils;

namespace ComicReaderUWP.SDK.Common.Lifecycle.Utils;

public sealed class MutableLiveDataWithMinInterval<T>(IMutableLiveData<T> liveData, long minInterval, int delay = 0) : IMutableLiveData<T>
{
    private readonly IMutableLiveData<T> _liveData = liveData;

    public void Clear()
    {
        _liveData.Clear();
    }

    public void Emit(T value)
    {
        _liveData.Emit(value);
    }

    public T? GetValue()
    {
        return _liveData.GetValue();
    }

    public void Observe(ILifecycleOwner owner, IObserver<T> observer, ObserveOptions options)
    {
        _liveData.Observe(owner, new ObserverWrapper<T>(owner, observer, minInterval, delay), options);
    }

    private class ObserverWrapper<U>(ILifecycleOwner owner, IObserver<U> observer, long minInterval, int delay) : IObserver<U>
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
            int timeRemaining = Math.Max((int)(minInterval - timeElapsed), delay);
            if (timeRemaining <= 0)
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
                    await Task.Delay(timeRemaining);
                    if (owner is not null && !owner.GetLifecycle().GetState().IsStarted())
                    {
                        return;
                    }

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
