// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Helpers.MenuFlyoutHelpers;

internal class MenuFlyoutSubItemViewModel(string text) : BaseMenuFlyoutItemViewModel
{
    public string Text { get; set; } = text;
    public string? Glyph { get; set; }
    public List<BaseMenuFlyoutItemViewModel> Items { get; set; } = [];

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

        foreach (BaseMenuFlyoutItemViewModel subItem in Items)
        {
            item.Items.Add(subItem.CreateMenuFlyoutItem());
        }

        return item;
    }
}
