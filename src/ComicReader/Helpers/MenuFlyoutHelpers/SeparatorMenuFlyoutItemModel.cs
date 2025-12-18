// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Helpers.MenuFlyoutHelpers;

internal class SeparatorMenuFlyoutItemModel : BaseMenuFlyoutItemModel
{
    protected override MenuFlyoutItemBase CreateMenuFlyoutItemInternal()
    {
        return new MenuFlyoutSeparator();
    }
}
