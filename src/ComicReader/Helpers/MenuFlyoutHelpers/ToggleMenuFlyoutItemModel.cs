// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Helpers.MenuFlyoutHelpers;

internal class ToggleMenuFlyoutItemModel(string text) : BaseMenuFlyoutItemModel
{
    public string Text { get; set; } = text;
    public bool IsChecked { get; set; } = false;
    public Action? Click { get; set; }

    protected override MenuFlyoutItemBase CreateMenuFlyoutItemInternal()
    {
        var item = new ToggleMenuFlyoutItem
        {
            Text = Text,
            IsChecked = IsChecked,
        };

        if (Click is not null)
        {
            item.Click += (sender, args) => Click.Invoke();
        }

        return item;
    }
}
