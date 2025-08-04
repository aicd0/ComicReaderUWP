// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Helpers.MenuFlyoutHelpers;

internal class MenuFlyoutItemViewModel(string text) : BaseMenuFlyoutItemViewModel
{
    public string Text { get; set; } = text;
    public IconElement? Icon { get; set; }
    public Action? OnClick { get; set; }

    protected override MenuFlyoutItemBase CreateMenuFlyoutItemInternal()
    {
        var item = new MenuFlyoutItem
        {
            Text = Text,
            Icon = Icon,
        };

        if (OnClick != null)
        {
            item.Click += (sender, args) => OnClick.Invoke();
        }

        return item;
    }
}
