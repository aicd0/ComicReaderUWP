// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ComicReaderUWP.Core.Common.Collections;

/// <summary>
/// An observable collection backed by a <see cref="RingBuffer{T}"/>, so that removing items from
/// the beginning (e.g. the oldest entries) is O(1) instead of O(N).
/// </summary>
public class ObservableRingCollection<T> : Collection<T>, INotifyCollectionChanged, INotifyPropertyChanged
{
    private const string COUNT_PROPERTY_NAME = "Count";
    private const string INDEXER_PROPERTY_NAME = "Item[]";

    private int _reentrancyBlockCount = 0;

    public ObservableRingCollection() : base(new RingBuffer<T>())
    {
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    protected override void InsertItem(int index, T item)
    {
        CheckReentrancy();
        base.InsertItem(index, item);
        RaisePropertyChanged(COUNT_PROPERTY_NAME);
        RaisePropertyChanged(INDEXER_PROPERTY_NAME);
        RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
    }

    protected override void RemoveItem(int index)
    {
        CheckReentrancy();
        T item = Items[index];
        base.RemoveItem(index);
        RaisePropertyChanged(COUNT_PROPERTY_NAME);
        RaisePropertyChanged(INDEXER_PROPERTY_NAME);
        RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
    }

    protected override void SetItem(int index, T item)
    {
        CheckReentrancy();
        T oldItem = Items[index];
        base.SetItem(index, item);
        RaisePropertyChanged(INDEXER_PROPERTY_NAME);
        RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, oldItem, index));
    }

    protected override void ClearItems()
    {
        CheckReentrancy();
        base.ClearItems();
        RaisePropertyChanged(COUNT_PROPERTY_NAME);
        RaisePropertyChanged(INDEXER_PROPERTY_NAME);
        RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    private void RaisePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void RaiseCollectionChanged(NotifyCollectionChangedEventArgs args)
    {
        NotifyCollectionChangedEventHandler? handler = CollectionChanged;
        if (handler is null)
        {
            return;
        }

        try
        {
            _reentrancyBlockCount++;
            handler(this, args);
        }
        finally
        {
            _reentrancyBlockCount--;
        }
    }

    private void CheckReentrancy()
    {
        if (_reentrancyBlockCount > 0)
        {
            throw new InvalidOperationException("Cannot change the collection during a CollectionChanged event.");
        }
    }
}
