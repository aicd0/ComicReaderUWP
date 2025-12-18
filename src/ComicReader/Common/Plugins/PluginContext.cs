// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.SDK.Plugins;
using ComicReader.SDK.Plugins.Comic;
using ComicReader.SDK.Plugins.Menu;

namespace ComicReader.Common.Plugins;

internal class PluginContext(IPlugin plugin) : IPluginContext
{
    public IPlugin Plugin => plugin;

    private readonly string _pluginName = plugin.Name;

    //
    // Main Page More Menu Items
    //

    private readonly List<IMenuItem> _mainPageMoreMenuItems = [];

    public void RegisterMainPageMoreMenuItem(IMenuItem? item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_mainPageMoreMenuItems)
        {
            _mainPageMoreMenuItems.Add(item);
        }
    }

    public IReadOnlyList<BaseMenuFlyoutItemModel> GetMainPageMoreMenuItems()
    {
        lock (_mainPageMoreMenuItems)
        {
            return [.. _mainPageMoreMenuItems.Select(CreateHostMenuFlyoutItem)];
        }
    }

    //
    // Comic Edited Handlers
    //

    private IComicEditedHandler? _comicEditedHandlers = null;

    public void RegisterComicEditedHandler(IComicEditedHandler? handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (_comicEditedHandlers is not null)
        {
            throw new InvalidOperationException($"Already registered: '{_pluginName}'");
        }

        _comicEditedHandlers = handler;
    }

    public void DispatchComicEditedEvent(IComicModel comic)
    {
        _comicEditedHandlers?.ComicEdited(comic);
    }

    //
    // Helpers
    //

    private static BaseMenuFlyoutItemModel CreateHostMenuFlyoutItem(IMenuItem item)
    {
        return item switch
        {
            SimpleMenuItem simpleMenuItem => new SimpleMenuFlyoutItemModel(simpleMenuItem.Text)
            {
                Glyph = simpleMenuItem.Glyph,
                IsEnabled = simpleMenuItem.IsEnabled,
                Click = simpleMenuItem.Click,
            },
            SeparatorMenuItem => new SeparatorMenuFlyoutItemModel(),
            ToggleMenuItem toggleMenuItem => new ToggleMenuFlyoutItemModel(toggleMenuItem.Text)
            {
                IsChecked = toggleMenuItem.IsChecked,
                Click = toggleMenuItem.Click,
            },
            SubItemMenuItem subItemMenuItem => new SubItemMenuFlyoutItemModel(subItemMenuItem.Text)
            {
                Glyph = subItemMenuItem.Glyph,
                Items = [.. subItemMenuItem.Items.Select(CreateHostMenuFlyoutItem)],
            },
            _ => throw new NotSupportedException($"Unsupported menu item type: {item.GetType().FullName}"),
        };
    }
}
