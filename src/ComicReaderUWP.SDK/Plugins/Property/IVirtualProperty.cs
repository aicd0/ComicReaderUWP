// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Plugins.Property;

public interface IVirtualProperty<A>
{
    string Name { get; }

    string DisplayName { get; }

    void Initialize<T>(IEnumerable<T> items) where T : A;

    IEnumerable<T> SortItems<T>(IEnumerable<T> items) where T : A;

    IEnumerable<T> SortGroups<T>(IEnumerable<T> items) where T : IItemGroup<A>;

    double? AsNumber(A item);

    IEnumerable<string> GetGroupNames(A item);
}
