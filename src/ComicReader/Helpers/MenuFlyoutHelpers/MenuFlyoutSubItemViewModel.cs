// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Helpers.MenuFlyoutHelpers;

internal class MenuFlyoutSubItemViewModel(string text) : BaseMenuFlyoutItemViewModel
{
    public string Text { get; set; } = text;
    public IconElement? Icon { get; set; }
    public List<BaseMenuFlyoutItemViewModel> Items { get; set; } = [];

    protected override MenuFlyoutItemBase CreateMenuFlyoutItemInternal()
    {
        var item = new MenuFlyoutSubItem
        {
            Text = Text,
            Icon = Icon,
        };

        foreach (BaseMenuFlyoutItemViewModel subItem in Items)
        {
            item.Items.Add(subItem.CreateMenuFlyoutItem());
        }

        return item;
    }
}
