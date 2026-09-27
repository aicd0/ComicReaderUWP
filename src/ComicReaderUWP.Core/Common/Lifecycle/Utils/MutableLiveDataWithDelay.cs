// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.Utils;

namespace ComicReaderUWP.Core.Common.Lifecycle.Utils;

public sealed class MutableLiveDataWithDelay<T>(
    IMutableLiveData<T> liveData,
    long minInterval,
    int delay = 0,
    Func<T, T, T>? mergeFunc = null) : IMutableLiveData<T> where T : notnull
{
    private readonly IMutableLiveData<T> _liveData = liveData;
    private readonly Lock _lock = new();

    private long _lastEmitTime = 0L;
    private T? _pendingValue = default;
    private bool _emitScheduled = false;

    public bool HasValue => _liveData.HasValue;

    public T? Value => _liveData.Value;

    public void Clear()
    {
        lock (_lock)
        {
            _pendingValue = default;
            _emitScheduled = false;
        }

        _liveData.Clear();
    }

    public void Emit(T value)
    {
        int timeRemaining;

        lock (_lock)
        {
            if (_emitScheduled)
            {
                _pendingValue = mergeFunc is not null ? mergeFunc(_pendingValue!, value) : value;
                return;
            }

            _emitScheduled = true;
            _pendingValue = value;

            long currentTime = GetTick();
            long timeElapsed = currentTime - _lastEmitTime;
            timeRemaining = Math.Max((int)(minInterval - timeElapsed), delay);
        }

        if (timeRemaining <= 0)
        {
            EmitPending();
            return;
        }

        CoroutineUtils.Run(async () =>
        {
            await Task.Delay(timeRemaining);
            EmitPending();
        });
    }

    public void Observe(ILifecycleOwner owner, IValueObserver<T> observer, ObserveOptions options)
    {
        _liveData.Observe(owner, observer, options);
    }

    public void RemoveObserver(IValueObserver<T> observer)
    {
        _liveData.RemoveObserver(observer);
    }

    public bool HasObserver(IValueObserver<T> observer)
    {
        return _liveData.HasObserver(observer);
    }

    private void EmitPending()
    {
        T value;

        lock (_lock)
        {
            if (!_emitScheduled)
            {
                return;
            }

            value = _pendingValue!;
            _pendingValue = default;
            _emitScheduled = false;
            _lastEmitTime = GetTick();
        }

        _liveData.Emit(value);
    }

    private static long GetTick()
    {
        return Environment.TickCount64;
    }
}
