// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReaderUWP.Converters;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Helpers.MenuFlyoutHelpers;

internal class SubItemMenuFlyoutItemModel() : BaseMenuFlyoutItemModel
{
    public required string Text { get; set; }
    public IconSource? Icon { get; set; }
    public IEnumerable<BaseMenuFlyoutItemModel> Items { get; set; } = [];

    protected override MenuFlyoutItemBase CreateMenuFlyoutItemInternal()
    {
        var item = new MenuFlyoutSubItem
        {
            Text = Text,
            Icon = IconSourceToIconElementConverter.Convert(Icon),
        };

        foreach (BaseMenuFlyoutItemModel subItem in Items)
        {
            item.Items.Add(subItem.CreateMenuFlyoutItem());
        }

        return item;
    }
}
