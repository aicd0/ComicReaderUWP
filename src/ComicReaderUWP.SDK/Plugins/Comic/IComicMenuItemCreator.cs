// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.SDK.Plugins.UI;
using ComicReaderUWP.SDK.Plugins.UI.Menu;

namespace ComicReaderUWP.SDK.Plugins.Comic;

public interface IComicMenuItemCreator
{
    IEnumerable<IMenuItem> CreateMenuItems(IUIContext uiContext, IComicModel primary, IEnumerable<IComicModel> selection);
}
