// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Specialized;

using ComicReaderUWP.Core.Common.Collections;

namespace ComicReaderUWP.Core.Tests;

[TestFixture]
public class ObservableRingCollectionTests
{
    [Test]
    public void AddRaisesNotifications()
    {
        ObservableRingCollection<int> collection = [];
        List<string> propertyNames = [];
        List<NotifyCollectionChangedEventArgs> changes = [];
        collection.PropertyChanged += (_, args) => propertyNames.Add(args.PropertyName ?? string.Empty);
        collection.CollectionChanged += (_, args) => changes.Add(args);

        collection.Add(10);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(collection, Is.EqualTo(new[] { 10 }));
            Assert.That(propertyNames, Is.EqualTo(new[] { "Count", "Item[]" }));
            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].Action, Is.EqualTo(NotifyCollectionChangedAction.Add));
            Assert.That(changes[0].NewStartingIndex, Is.EqualTo(0));
            Assert.That(changes[0].NewItems, Is.EqualTo(new[] { 10 }));
        }
    }

    [Test]
    public void InsertRaisesAddWithIndex()
    {
        ObservableRingCollection<int> collection = [1, 2, 3];
        List<NotifyCollectionChangedEventArgs> changes = [];
        collection.CollectionChanged += (_, args) => changes.Add(args);

        collection.Insert(1, 10);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(collection, Is.EqualTo(new[] { 1, 10, 2, 3 }));
            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].Action, Is.EqualTo(NotifyCollectionChangedAction.Add));
            Assert.That(changes[0].NewStartingIndex, Is.EqualTo(1));
            Assert.That(changes[0].NewItems, Is.EqualTo(new[] { 10 }));
        }
    }

    [Test]
    public void RemoveRaisesRemoveWithItemAndIndex()
    {
        ObservableRingCollection<int> collection = [1, 2, 3];
        List<string> propertyNames = [];
        List<NotifyCollectionChangedEventArgs> changes = [];
        collection.PropertyChanged += (_, args) => propertyNames.Add(args.PropertyName ?? string.Empty);
        collection.CollectionChanged += (_, args) => changes.Add(args);

        collection.RemoveAt(0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(collection, Is.EqualTo(new[] { 2, 3 }));
            Assert.That(propertyNames, Is.EqualTo(new[] { "Count", "Item[]" }));
            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].Action, Is.EqualTo(NotifyCollectionChangedAction.Remove));
            Assert.That(changes[0].OldStartingIndex, Is.EqualTo(0));
            Assert.That(changes[0].OldItems, Is.EqualTo(new[] { 1 }));
        }
    }

    [Test]
    public void IndexerSetRaisesReplace()
    {
        ObservableRingCollection<int> collection = [1, 2, 3];
        List<string> propertyNames = [];
        List<NotifyCollectionChangedEventArgs> changes = [];
        collection.PropertyChanged += (_, args) => propertyNames.Add(args.PropertyName ?? string.Empty);
        collection.CollectionChanged += (_, args) => changes.Add(args);

        collection[1] = 20;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(collection, Is.EqualTo(new[] { 1, 20, 3 }));
            Assert.That(propertyNames, Is.EqualTo(new[] { "Item[]" }));
            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].Action, Is.EqualTo(NotifyCollectionChangedAction.Replace));
            Assert.That(changes[0].OldItems, Is.EqualTo(new[] { 2 }));
            Assert.That(changes[0].NewItems, Is.EqualTo(new[] { 20 }));
        }
    }

    [Test]
    public void ClearRaisesReset()
    {
        ObservableRingCollection<int> collection = [1, 2, 3];
        List<string> propertyNames = [];
        List<NotifyCollectionChangedEventArgs> changes = [];
        collection.PropertyChanged += (_, args) => propertyNames.Add(args.PropertyName ?? string.Empty);
        collection.CollectionChanged += (_, args) => changes.Add(args);

        collection.Clear();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(collection, Is.Empty);
            Assert.That(propertyNames, Is.EqualTo(new[] { "Count", "Item[]" }));
            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].Action, Is.EqualTo(NotifyCollectionChangedAction.Reset));
        }
    }

    [Test]
    public void RemovingFromFrontKeepsOrder()
    {
        ObservableRingCollection<int> collection = [];

        for (int i = 0; i < 100; ++i)
        {
            collection.Add(i);
        }

        // Mirrors the log page trimming its oldest entries.
        while (collection.Count > 20)
        {
            collection.RemoveAt(0);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(collection.Count, Is.EqualTo(20));
            for (int i = 0; i < 20; ++i)
            {
                Assert.That(collection[i], Is.EqualTo(80 + i));
            }
        }
    }

    [Test]
    public void MutationInsideHandlerThrows()
    {
        ObservableRingCollection<int> collection = [1, 2, 3];
        collection.CollectionChanged += (_, _) => collection.Add(4);

        Assert.That(() => collection.Add(0), Throws.TypeOf<InvalidOperationException>());
    }
}
