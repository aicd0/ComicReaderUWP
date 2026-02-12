// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Plugins.Property;

public interface IVirtualPropertySorter<A>
{
    IEnumerable<T> SortItems<T>(IEnumerable<T> items) where T : A;

    IEnumerable<T> SortGroups<T>(IEnumerable<T> items) where T : IItemGroup<A>;

    IEnumerable<string> GetGroupNames(A item);

    double? AsNumber(A item);
}
