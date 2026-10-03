// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReaderUWP.Common.HotKey;
using ComicReaderUWP.Converters;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Helpers.MenuFlyoutHelpers;

internal class SimpleMenuFlyoutItemModel : BaseMenuFlyoutItemModel
{
    public required string Text { get; set; }
    public IconSource? Icon { get; set; }
    public bool IsEnabled { get; set; } = true;
    public Action? Click { get; set; }
    public string? ShortcutAction { get; set; }

    protected override MenuFlyoutItemBase CreateMenuFlyoutItemInternal()
    {
        var item = new MenuFlyoutItem
        {
            Text = Text,
            Icon = IconSourceToIconElementConverter.Convert(Icon),
            IsEnabled = IsEnabled,
        };

        if (ShortcutAction is not null)
        {
            string shortcutText = KeyboardShortcutManager.GetShortcutDisplayText(ShortcutAction);
            if (!string.IsNullOrEmpty(shortcutText))
            {
                item.KeyboardAcceleratorTextOverride = shortcutText;
            }
        }

        if (Click != null)
        {
            item.Click += (sender, args) => Click.Invoke();
        }

        return item;
    }
}
