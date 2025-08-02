// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

using ComicReader.SDK.Common.Algorithm;

namespace ComicReader.ViewModels;

internal partial class SimpleGroupViewModel<T> : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the name of the group.
    /// </summary>
    public string GroupName { get; private set; }

    /// <summary>
    /// Items in the group.
    /// </summary>
    /// <remarks>
    /// Do not modify this collection directly. Use <see cref="UpdateItems"/> to set the items.
    /// </remarks>
    public List<T> Items { get; } = [];

    /// <summary>
    /// Items to display in the group.
    /// </summary>
    /// <remarks>
    /// Do not modify this collection directly. Use <see cref="UpdateItems"/> to set the items.
    /// </remarks>
    public ObservableCollection<T> DisplayItems { get; } = [];

    private bool _collaped = false;
    /// <summary>
    /// Gets or sets a value indicating whether the collection is collapsed.
    /// </summary>
    /// <remarks>When set to <see langword="true"/>, the <c>DisplayItems</c> collection is cleared.  When set
    /// to <see langword="false"/>, the <c>DisplayItems</c> collection is populated with items from the <c>Items</c>
    /// collection.</remarks>
    public bool Collapsed
    {
        get => _collaped;
        set
        {
            _collaped = value;
            NotifyPropertyChanged(nameof(Collapsed));

            if (value)
            {
                DisplayItems.Clear();
            }
            else
            {
                DisplayItems.Clear();
                foreach (T item in Items)
                {
                    DisplayItems.Add(item);
                }
            }
        }
    }

    public SimpleGroupViewModel(string groupName, IEnumerable<T> items, bool collapsed)
    {
        GroupName = groupName;

        Items.AddRange(items);
        _collaped = collapsed;
        if (!collapsed)
        {
            foreach (T item in items)
            {
                DisplayItems.Add(item);
            }
        }
    }

    /// <summary>
    /// Updates the current list of items with the specified collection and refreshes the display items if necessary.
    /// </summary>
    /// <remarks>If the collection is not collapsed, the display items are updated using the provided comparer
    /// function to determine item equality.</remarks>
    /// <param name="items">The new collection of items to update the current list with. This collection must be read-only.</param>
    /// <param name="comparer">A function that compares two items and returns <see langword="true"/> if they are considered equal; otherwise,
    /// <see langword="false"/>.</param>
    /// <param name="updater">A function that updates the first item with the second item if they are considered equal.</param>
    public void UpdateItems(IReadOnlyList<T> items, Func<T, T, bool> comparer, Action<T, T> updater)
    {
        Items.Clear();
        Items.AddRange(items);

        if (!_collaped)
        {
            DiffUtils.UpdateCollection(DisplayItems, items, comparer, updater);
        }
    }

    /// <summary>
    /// Updates the items in the collection by applying a specified replacement function.
    /// </summary>
    /// <remarks>This method iterates over two collections, updating each item with the result of the
    /// <paramref name="replacement"/> function. If the function returns a non-null value, the item is replaced;
    /// otherwise, it remains unchanged.</remarks>
    /// <param name="replacement">A function that takes an item of type <typeparamref name="T"/> and returns a new item of the same type. If the
    /// function returns <see langword="null"/>, the item is not replaced.</param>
    public void UpdateItems(Func<T, T?> replacement)
    {
        for (int i = 0; i < Items.Count; i++)
        {
            T oldItem = Items[i];
            T? newItem = replacement(oldItem);
            if (newItem is not null)
            {
                Items[i] = newItem;
            }
        }

        for (int i = 0; i < DisplayItems.Count; i++)
        {
            T oldItem = DisplayItems[i];
            T? newItem = replacement(oldItem);
            if (newItem is not null)
            {
                DisplayItems[i] = newItem;
            }
        }
    }

    protected void NotifyPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
