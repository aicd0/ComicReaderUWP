// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.Utils;

namespace ComicReaderUWP.Core.Common.Lifecycle.Utils;

public sealed class MutableLiveDataWithDelay<T>(
    IMutableLiveData<T> liveData,
    int minInterval,
    int delay = 0,
    Func<T, T, T>? mergeFunc = null) : IMutableLiveData<T> where T : notnull
{
    private readonly IMutableLiveData<T> _liveData = liveData;
    private readonly int _minInterval = minInterval;
    private readonly int _delay = delay;

    private readonly Lock _lock = new();
    private long _lastEmitTime = 0L;
    private long _pendingDeadline = 0L;
    private long _emitVersion = 0L;
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
        EmitInternal(value, 0);
    }

    public void EmitDelayed(T value, int delay)
    {
        EmitInternal(value, delay);
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

    private void EmitInternal(T value, int delay)
    {
        long version;
        int timeRemaining;

        lock (_lock)
        {
            _pendingValue = _emitScheduled && mergeFunc is not null ? mergeFunc(_pendingValue!, value) : value;

            long currentTime = GetTick();
            int requiredDelay = Math.Max(_delay, delay);
            long deadline = Math.Max(_lastEmitTime + _minInterval, currentTime + requiredDelay);

            if (_emitScheduled)
            {
                if (deadline >= _pendingDeadline)
                {
                    return;
                }
            }
            else
            {
                _emitScheduled = true;
            }

            _pendingDeadline = deadline;
            version = ++_emitVersion;
            timeRemaining = (int)Math.Clamp(deadline - currentTime, int.MinValue, int.MaxValue);
        }

        if (timeRemaining <= 0)
        {
            EmitPending(version);
            return;
        }

        CoroutineUtils.Run(async () =>
        {
            await Task.Delay(timeRemaining);
            EmitPending(version);
        });
    }

    private void EmitPending(long version)
    {
        T value;

        lock (_lock)
        {
            if (!_emitScheduled || _emitVersion != version)
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
