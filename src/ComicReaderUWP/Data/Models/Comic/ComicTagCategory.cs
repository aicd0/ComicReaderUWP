// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace ComicReaderUWP.Data.Models.Comic;

internal class ComicTagCategory(HashSet<string> tags) : SDK.Plugins.Comic.IComicTagCategory
{
    public IReadOnlySet<string> Tags { get; } = tags;

    //
    // SDK.Plugins.Comic.IComicTagCategory Implementation
    //

    IReadOnlySet<string> SDK.Plugins.Comic.IComicTagCategory.Tags => Tags;
}
