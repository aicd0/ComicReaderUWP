// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Plugins.UI.Menu;

public interface ICommonMenuItemCreator
{
    IEnumerable<IMenuItem> CreateMenuItems(IWindowContext windowContext);
}
