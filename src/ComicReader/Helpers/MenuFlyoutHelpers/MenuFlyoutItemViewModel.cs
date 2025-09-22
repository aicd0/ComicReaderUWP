// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Helpers.MenuFlyoutHelpers;

internal class MenuFlyoutItemViewModel(string text) : BaseMenuFlyoutItemViewModel
{
    public string Text { get; set; } = text;
    public string? Glyph { get; set; }
    public bool IsEnabled { get; set; } = true;
    public Action? OnClick { get; set; }

    protected override MenuFlyoutItemBase CreateMenuFlyoutItemInternal()
    {
        var item = new MenuFlyoutItem
        {
            Text = Text,
            Icon = string.IsNullOrEmpty(Glyph) ? null : new FontIcon
            {
                Glyph = Glyph,
            },
            IsEnabled = IsEnabled,
        };

        if (OnClick != null)
        {
            item.Click += (sender, args) => OnClick.Invoke();
        }

        return item;
    }
}
