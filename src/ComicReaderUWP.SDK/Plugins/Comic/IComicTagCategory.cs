// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Plugins.Comic;

public interface IComicTagCategory
{
    string Name { get; }

    IReadOnlySet<string> Tags { get; }
}
