// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Plugins.Menu;

namespace ComicReader.SDK.Plugins.Comic;

public interface IComicMenuItemCreator
{
    IEnumerable<IMenuItem> CreateMenuItems(IComicModel primary, IEnumerable<IComicModel> selection);
}
