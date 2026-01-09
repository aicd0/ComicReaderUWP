// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Helpers.MenuFlyoutHelpers;

internal class SimpleMenuFlyoutItemModel : BaseMenuFlyoutItemModel
{
    public required string Text { get; set; }
    public string? Glyph { get; set; }
    public bool IsEnabled { get; set; } = true;
    public Action? Click { get; set; }

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

        if (Click != null)
        {
            item.Click += (sender, args) => Click.Invoke();
        }

        return item;
    }
}
