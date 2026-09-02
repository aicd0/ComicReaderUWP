// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.ObjectModel;

namespace ComicReaderUWP.Common.Legacy;

interface IC1<out T> { }
public class C1<T> : IC1<T>
{
    public static void NotifyCollectionChanged(ObservableCollection<T> collection, T item)
    {
        int idx = collection.IndexOf(item);
        collection.Insert(idx + 1, item);
        collection.RemoveAt(idx);
    }
}
