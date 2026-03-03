// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Helpers.MenuFlyoutHelpers;

internal class SubItemMenuFlyoutItemModel() : BaseMenuFlyoutItemModel
{
    public required string Text { get; set; }
    public string? Glyph { get; set; }
    public IEnumerable<BaseMenuFlyoutItemModel> Items { get; set; } = [];

    protected override MenuFlyoutItemBase CreateMenuFlyoutItemInternal()
    {
        var item = new MenuFlyoutSubItem
        {
            Text = Text,
            Icon = string.IsNullOrEmpty(Glyph) ? null : new FontIcon
            {
                Glyph = Glyph,
            },
        };

        foreach (BaseMenuFlyoutItemModel subItem in Items)
        {
            item.Items.Add(subItem.CreateMenuFlyoutItem());
        }

        return item;
    }
}
