// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.Collections;

namespace ComicReaderUWP.Core.Tests;

[TestFixture]
public class RingBufferTests
{
    [Test]
    public void AddAndIndex()
    {
        RingBuffer<int> buffer = [];
        Assert.That(buffer.Count, Is.Zero);
        Assert.That(buffer.IsReadOnly, Is.False);

        for (int i = 0; i < 10; ++i)
        {
            buffer.Add(i);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(buffer.Count, Is.EqualTo(10));
            for (int i = 0; i < 10; ++i)
            {
                Assert.That(buffer[i], Is.EqualTo(i));
            }
        }
    }

    [Test]
    public void IndexerSetReplacesValue()
    {
        RingBuffer<int> buffer = [1, 2, 3];

        buffer[1] = 20;

        Assert.That(buffer, Is.EqualTo(new[] { 1, 20, 3 }));
    }

    [Test]
    public void IndexerOutOfRangeThrows()
    {
        RingBuffer<int> buffer = [1, 2, 3];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => buffer[-1], Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => buffer[3], Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => buffer[3] = 0, Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }

    [Test]
    public void InsertShiftsElements()
    {
        RingBuffer<int> buffer = [1, 2, 3];

        buffer.Insert(0, 10);
        buffer.Insert(2, 20);
        buffer.Insert(5, 30);

        Assert.That(buffer, Is.EqualTo(new[] { 10, 1, 20, 2, 3, 30 }));
    }

    [Test]
    public void InsertOutOfRangeThrows()
    {
        RingBuffer<int> buffer = [1, 2, 3];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => buffer.Insert(-1, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => buffer.Insert(4, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }

    [Test]
    public void RemoveAtShiftsElements()
    {
        RingBuffer<int> buffer = [0, 1, 2, 3, 4];

        buffer.RemoveAt(2);
        Assert.That(buffer, Is.EqualTo(new[] { 0, 1, 3, 4 }));

        buffer.RemoveAt(3);
        Assert.That(buffer, Is.EqualTo(new[] { 0, 1, 3 }));

        buffer.RemoveAt(0);
        Assert.That(buffer, Is.EqualTo(new[] { 1, 3 }));
    }

    [Test]
    public void RemoveAtOutOfRangeThrows()
    {
        RingBuffer<int> buffer = [1, 2, 3];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => buffer.RemoveAt(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => buffer.RemoveAt(3), Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }

    [Test]
    public void RemoveFindsAndRemovesItem()
    {
        RingBuffer<int> buffer = [1, 2, 3];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(buffer.Remove(2), Is.True);
            Assert.That(buffer.Remove(2), Is.False);
            Assert.That(buffer, Is.EqualTo(new[] { 1, 3 }));
        }
    }

    [Test]
    public void ContainsAndIndexOf()
    {
        RingBuffer<int> buffer = [1, 2, 3];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(buffer.Contains(2), Is.True);
            Assert.That(buffer.Contains(4), Is.False);
            Assert.That(buffer.IndexOf(3), Is.EqualTo(2));
            Assert.That(buffer.IndexOf(4), Is.EqualTo(-1));
        }
    }

    [Test]
    public void ClearResetsBuffer()
    {
        RingBuffer<int> buffer = [1, 2, 3];

        buffer.Clear();

        Assert.That(buffer.Count, Is.Zero);

        buffer.Add(7);
        Assert.That(buffer, Is.EqualTo(new[] { 7 }));
    }

    [Test]
    public void ReusesSpaceAfterWrapAround()
    {
        RingBuffer<int> buffer = [];

        // Fill and drain repeatedly to force the head to wrap around the ring.
        for (int i = 0; i < 32; ++i)
        {
            buffer.Add(i);
            buffer.RemoveAt(0);
        }

        buffer.Add(100);
        buffer.Add(101);
        buffer.RemoveAt(0);

        Assert.That(buffer, Is.EqualTo(new[] { 101 }));
    }

    [Test]
    public void CopyToWritesItemsInOrder()
    {
        RingBuffer<int> buffer = [0, 1, 2, 3];
        buffer.RemoveAt(0);
        buffer.RemoveAt(0);
        buffer.Add(4);
        buffer.Add(5);

        int[] array = new int[6];
        buffer.CopyTo(array, 1);

        Assert.That(array, Is.EqualTo(new[] { 0, 2, 3, 4, 5, 0 }));
    }

    [Test]
    public void CopyToValidatesArguments()
    {
        RingBuffer<int> buffer = [1, 2, 3];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => buffer.CopyTo(null!, 0), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => buffer.CopyTo(new int[5], -1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => buffer.CopyTo(new int[5], 6), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => buffer.CopyTo(new int[2], 0), Throws.TypeOf<ArgumentException>());
        }
    }

    [Test]
    public void EnumeratorYieldsItemsInOrder()
    {
        RingBuffer<int> buffer = [0, 1, 2, 3];
        buffer.RemoveAt(0);
        buffer.Add(4); // Wrap the head around the ring.

        List<int> values = [.. buffer];

        Assert.That(values, Is.EqualTo(new[] { 1, 2, 3, 4 }));
    }

    [Test]
    public void EnumeratorThrowsWhenCollectionChanges()
    {
        RingBuffer<int> buffer = [1, 2, 3];

        using IEnumerator<int> enumerator = buffer.GetEnumerator();
        Assert.That(enumerator.MoveNext(), Is.True);

        buffer.Add(4);

        Assert.That(() => enumerator.MoveNext(), Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void RandomOperationsMatchReferenceList()
    {
        const int stepCount = 5000;
        const int maxCount = 256;

        RingBuffer<int> buffer = [];
        List<int> reference = [];
        Random random = new(20260930);

        int nextValue = 0;
        for (int step = 0; step < stepCount; ++step)
        {
            int action = reference.Count == 0 ? 0 : random.Next(5);
            switch (action)
            {
                case 0:
                case 1:
                    if (reference.Count >= maxCount)
                    {
                        buffer.RemoveAt(0);
                        reference.RemoveAt(0);
                    }

                    buffer.Add(nextValue);
                    reference.Add(nextValue);
                    ++nextValue;
                    break;
                case 2:
                    buffer.RemoveAt(0);
                    reference.RemoveAt(0);
                    break;
                case 3:
                    {
                        int index = random.Next(reference.Count);
                        buffer.RemoveAt(index);
                        reference.RemoveAt(index);
                        break;
                    }
                default:
                    {
                        int index = random.Next(reference.Count + 1);
                        buffer.Insert(index, nextValue);
                        reference.Insert(index, nextValue);
                        ++nextValue;
                        break;
                    }
            }

            Assert.That(buffer, Is.EqualTo(reference), $"Mismatch at step {step}.");
        }
    }
}
