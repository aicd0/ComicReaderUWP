// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace ComicReaderUWP.Common.Utils;

internal sealed partial class RefCounted<T>(T value) : IDisposable where T : IDisposable
{
    private T _value = value ?? throw new ArgumentNullException(nameof(value));
    private int _refCount = 1;
    private int _disposed; // 0 = alive, 1 = disposed

    /// <summary>
    /// Gets the wrapped value. Throws if already disposed.
    /// </summary>
    public T Value
    {
        get
        {
            return Volatile.Read(ref _disposed) == 1 ? throw new ObjectDisposedException(nameof(RefCounted<T>)) : _value;
        }
    }

    /// <summary>
    /// Increment reference count.
    /// </summary>
    public void Ref()
    {
        while (true)
        {
            int current = Volatile.Read(ref _refCount);
            ObjectDisposedException.ThrowIf(current == 0, nameof(RefCounted<T>));

            if (Interlocked.CompareExchange(ref _refCount, current + 1, current) == current)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Try to increment reference count. Returns false if already disposed.
    /// </summary>
    public bool TryRef([NotNullWhen(true)] out T? value)
    {
        while (true)
        {
            int current = Volatile.Read(ref _refCount);
            if (current == 0)
            {
                value = default;
                return false;
            }

            if (Interlocked.CompareExchange(ref _refCount, current + 1, current) == current)
            {
                value = _value;
                return true;
            }
        }
    }

    /// <summary>
    /// Decrement reference count. Disposes the value when it reaches zero.
    /// </summary>
    public void Unref()
    {
        int newCount = Interlocked.Decrement(ref _refCount);

        if (newCount < 0)
        {
            throw new InvalidOperationException("Unref called more times than Ref.");
        }

        if (newCount == 0)
        {
            DisposeInternal();
        }
    }

    /// <summary>
    /// Equivalent to Unref() for IDisposable compatibility.
    /// </summary>
    public void Dispose()
    {
        Unref();
    }

    private void DisposeInternal()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _value.Dispose();
        _value = default!;
    }
}
