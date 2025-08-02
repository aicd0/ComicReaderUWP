// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.Common.Utils;

internal class ConcurrentWeakPool<K, V> where K : notnull where V : class
{
    private const string TAG = nameof(ConcurrentWeakPool<K, V>);

    private readonly ConcurrentDictionary<K, WeakReference<V>> _pool = new();
    private int _cleaning = 0;
    private long _lastCleanupTime = 0;

    public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value)
    {
        ArgumentNullException.ThrowIfNull(key);

        CleanupIfNeeded();

        if (_pool.TryGetValue(key, out WeakReference<V>? valueRef) && valueRef.TryGetTarget(out value))
        {
            return true;
        }
        value = null;
        return false;
    }

    public V GetOrAdd(K key, V value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        CleanupIfNeeded();

        WeakReference<V> valueRef = new(value);
        for (int i = 0; i < 10; ++i)
        {
            WeakReference<V> existingRef = _pool.GetOrAdd(key, valueRef);
            if (existingRef.TryGetTarget(out V? existingValue))
            {
                return existingValue;
            }
            if (_pool.TryUpdate(key, valueRef, existingRef))
            {
                return value;
            }
        }

        Logger.F(TAG, "Failed to update the pool after multiple attempts.");
        lock (_pool)
        {
            if (_pool.TryGetValue(key, out WeakReference<V>? existingRef) && existingRef.TryGetTarget(out V? existingValue))
            {
                return existingValue;
            }
            _pool[key] = valueRef;
            return value;
        }
    }

    public bool Remove(K key)
    {
        ArgumentNullException.ThrowIfNull(key);
        CleanupIfNeeded();
        return _pool.TryRemove(key, out _);
    }

    private void CleanupIfNeeded()
    {
        if (_pool.Count <= 256)
        {
            return;
        }
        if (_cleaning != 0 || Interlocked.CompareExchange(ref _cleaning, 1, 0) != 0)
        {
            return;
        }
        try
        {
            long tick = GetTick();
            if (tick - _lastCleanupTime < 30000)
            {
                return;
            }
            _lastCleanupTime = tick;

            var kvpToRemove = new List<KeyValuePair<K, WeakReference<V>>>();
            foreach (KeyValuePair<K, WeakReference<V>> kvp in _pool)
            {
                if (!kvp.Value.TryGetTarget(out _))
                {
                    kvpToRemove.Add(kvp);
                }
            }
            foreach (KeyValuePair<K, WeakReference<V>> kvp in kvpToRemove)
            {
                _pool.TryRemove(kvp);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _cleaning, 0);
        }
    }

    private static long GetTick()
    {
        return Environment.TickCount64;
    }
}
