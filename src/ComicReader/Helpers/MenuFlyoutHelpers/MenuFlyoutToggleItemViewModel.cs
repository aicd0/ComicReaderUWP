// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Helpers.MenuFlyoutHelpers;

internal class MenuFlyoutToggleItemViewModel(string text) : BaseMenuFlyoutItemViewModel
{
    public string Text { get; set; } = text;
    public bool IsChecked { get; set; } = false;
    public Action? OnClick { get; set; }

    protected override MenuFlyoutItemBase CreateMenuFlyoutItemInternal()
    {
        var item = new ToggleMenuFlyoutItem
        {
            Text = Text,
            IsChecked = IsChecked,
        };

        if (OnClick is not null)
        {
            item.Click += (sender, args) => OnClick.Invoke();
        }

        return item;
    }
}
