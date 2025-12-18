// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Helpers.MenuFlyoutHelpers;

internal abstract class BaseMenuFlyoutItemModel
{
    public MenuFlyoutItemBase CreateMenuFlyoutItem()
    {
        return CreateMenuFlyoutItemInternal();
    }

    protected abstract MenuFlyoutItemBase CreateMenuFlyoutItemInternal();
}
