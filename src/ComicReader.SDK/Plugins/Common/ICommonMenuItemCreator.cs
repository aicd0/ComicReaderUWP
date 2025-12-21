// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Plugins.Menu;

namespace ComicReader.SDK.Plugins.Common;

public interface ICommonMenuItemCreator
{
    IEnumerable<IMenuItem> CreateMenuItems();
}
