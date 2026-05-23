// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

using ComicReaderUWP.Core.Common.Algorithm;

namespace ComicReaderUWP.Core.Tests;

[TestFixture]
public class DiffUtilsTests
{
    [Test]
    public void Test()
    {
        Func<int, int, bool> intComparer = (a, b) => a == b;
        Test([], [], intComparer);
        Test([], [1], intComparer);
        Test([], [1, 2, 3, 4, 5], intComparer);
        Test([1], [], intComparer);
        Test([1, 2, 3, 4, 5], [], intComparer);
        Test([1, 2, 3], [1, 2, 3], intComparer);
        Test([1, 2], [2, 1], intComparer);
        Test([1, 2, 3], [3, 2, 1], intComparer);
        Test([1, 2, 3], [3, 4, 5], intComparer);
        Test([1, 2, 3, 4], [1, 3, 4, 5], intComparer);
        Test([1, 1, 1], [1, 1, 1], intComparer);
        Test([1, 2, 1], [1, 3, 1], intComparer);
        Test([1, 2, 1], [1, 1, 2], intComparer);
        Test([1, 1, 1], [1, 1, 1, 1], intComparer);
        Test([2, 1, 1, 1], [1, 1, 1], intComparer);
        Test([1, 2, 2, 3, 4, 5, 6, 7, 8, 9], [2, 4, 2, 8, 3, 7, 2, 5], intComparer);
    }

    private void Test<T>(List<T> fromCollection, List<T> toCollection, Func<T, T, bool> comparer)
    {
        ObservableCollection<T> f;
        List<T> t;

        f = [.. fromCollection];
        t = [.. toCollection];
        DiffUtils.UpdateCollectionUsingME(f, t, comparer);
        Assert.That(t, Has.Count.EqualTo(f.Count));
        for (int i = 0; i < f.Count; i++)
        {
            Assert.That(comparer(f[i], t[i]), Is.True);
        }

        f = [.. fromCollection];
        t = [.. toCollection];
        DiffUtils.UpdateCollectionUsingAF(f, t, comparer);
        Assert.That(t, Has.Count.EqualTo(f.Count));
        for (int i = 0; i < f.Count; i++)
        {
            Assert.That(comparer(f[i], t[i]), Is.True);
        }

        f = [.. fromCollection];
        t = [.. toCollection];
        DiffUtils.UpdateCollectionUsingDF(f, t, comparer);
        Assert.That(t, Has.Count.EqualTo(f.Count));
        for (int i = 0; i < f.Count; i++)
        {
            Assert.That(comparer(f[i], t[i]), Is.True);
        }
    }
}
