// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.SDK.Plugins.Menu;

namespace ComicReaderUWP.SDK.Plugins.Common;

public interface ICommonMenuItemCreator
{
    IEnumerable<IMenuItem> CreateMenuItems(IUIContext uiContext);
}
