// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Plugins.Property;

public interface IVirtualProperty<A>
{
    string Name { get; }

    string DisplayName { get; }

    IVirtualPropertySorter<A> CreateSorter<T>(IEnumerable<T> items) where T : A;
}
