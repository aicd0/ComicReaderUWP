// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections;

namespace ComicReaderUWP.Core.Common.Collections;

/// <summary>
/// A list backed by a ring buffer. Adding an item at the end and removing the item at index 0
/// (the head) are O(1); operations on other positions shift elements like a list does.
/// </summary>
public sealed class RingBuffer<T> : IList<T>
{
    private T[] _buffer = [];
    private int _head = 0;
    private int _count = 0;
    private int _version = 0;

    public int Count => _count;

    public bool IsReadOnly => false;

    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _buffer[(_head + index) % _buffer.Length];
        }
        set
        {
            if ((uint)index >= (uint)_count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            _buffer[(_head + index) % _buffer.Length] = value;
            _version++;
        }
    }

    public void Add(T item)
    {
        EnsureCapacity(_count + 1);
        _buffer[(_head + _count) % _buffer.Length] = item;
        _count++;
        _version++;
    }

    public void Insert(int index, T item)
    {
        if ((uint)index > (uint)_count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (index == _count)
        {
            Add(item);
            return;
        }

        EnsureCapacity(_count + 1);

        // Shift the tail part right by one slot.
        for (int i = _count; i > index; --i)
        {
            _buffer[(_head + i) % _buffer.Length] = _buffer[(_head + i - 1) % _buffer.Length];
        }
        _buffer[(_head + index) % _buffer.Length] = item;
        _count++;
        _version++;
    }

    public void RemoveAt(int index)
    {
        if ((uint)index >= (uint)_count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (index == 0)
        {
            // O(1): drop the head slot and advance the head.
            _buffer[_head] = default!;
            _head = (_head + 1) % _buffer.Length;
            _count--;
            _version++;
            return;
        }

        // Shift the tail part left by one slot.
        for (int i = index; i < _count - 1; ++i)
        {
            _buffer[(_head + i) % _buffer.Length] = _buffer[(_head + i + 1) % _buffer.Length];
        }
        _buffer[(_head + _count - 1) % _buffer.Length] = default!;
        _count--;
        _version++;
    }

    public void Clear()
    {
        Array.Clear(_buffer);
        _head = 0;
        _count = 0;
        _version++;
    }

    public bool Contains(T item)
    {
        return IndexOf(item) >= 0;
    }

    public int IndexOf(T item)
    {
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < _count; ++i)
        {
            if (comparer.Equals(_buffer[(_head + i) % _buffer.Length], item))
            {
                return i;
            }
        }

        return -1;
    }

    public bool Remove(T item)
    {
        int index = IndexOf(item);
        if (index < 0)
        {
            return false;
        }

        RemoveAt(index);
        return true;
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        if (arrayIndex < 0 || arrayIndex > array.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }
        if (array.Length - arrayIndex < _count)
        {
            throw new ArgumentException("The destination array is not long enough.", nameof(array));
        }

        for (int i = 0; i < _count; ++i)
        {
            array[arrayIndex + i] = _buffer[(_head + i) % _buffer.Length];
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        int version = _version;
        for (int i = 0; i < _count; ++i)
        {
            if (_version != version)
            {
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
            }

            yield return _buffer[(_head + i) % _buffer.Length];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private void EnsureCapacity(int required)
    {
        if (required <= _buffer.Length)
        {
            return;
        }

        int newLength = Math.Max(Math.Max(_buffer.Length * 2, required), 4);
        var newBuffer = new T[newLength];

        int firstPart = Math.Min(_count, _buffer.Length - _head);
        if (firstPart > 0)
        {
            Array.Copy(_buffer, _head, newBuffer, 0, firstPart);
            Array.Copy(_buffer, 0, newBuffer, firstPart, _count - firstPart);
        }

        _buffer = newBuffer;
        _head = 0;
    }
}
