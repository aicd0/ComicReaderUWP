// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Common.Plugins;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.SDK.Plugins.UI;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Views.Pages.Main.Sidebar;

internal sealed partial class SidebarView : BaseUserControl
{
    public const string ITEM_FAVORITES = "Favorites";
    public const string ITEM_HISTORY = "History";
    public const string ITEM_TAGS = "Tags";
    public const string ITEM_FOLDERS = "Folders";
    public const string ITEM_FILTER_PRESETS = "FilterPresets";
    public const string ITEM_PLAYLIST = "Playlist";
    public const string ITEM_COMIC_INFO = "ComicInfo";

    public delegate void PinStateChangedEventHandler(SidebarView sender, bool pinned);
    public event PinStateChangedEventHandler? PinStateChanged;

    private readonly SidebarViewModel ViewModel = new();

    private readonly List<SidebarPageItem> _internalItems;
    private readonly Dictionary<string, SidebarPageItem> _items = [];
    private ISidePaneHandler? _handler = null;
    private readonly Dictionary<string, object> _pageCache = [];
    private string _initialTag = string.Empty;
    private string _currentTag = string.Empty;

    public bool Pinned { get; private set; } = false;

    public SidebarView()
    {
        _internalItems =
        [
            new()
            {
                Tag = ITEM_COMIC_INFO,
                Name = StringResource.ComicInfo,
                PageRoute = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SIDE_PANE_COMIC_INFO),
                Icon = new FontIcon() { Glyph = "\uE946" },
            },
            new()
            {
                Tag = ITEM_PLAYLIST,
                Name = StringResource.ReadingList,
                PageRoute = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SIDE_PANE_PLAYLIST),
                Icon = new FontIcon() { Glyph = "\uE7BC" },
            },
            new()
            {
                Tag = ITEM_TAGS,
                Name = StringResource.Tags,
                PageRoute = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SIDE_PANE_TAGS),
                Icon = new FontIcon() { Glyph = "\uE8EC" },
            },
            new()
            {
                Tag = ITEM_FAVORITES,
                Name = StringResource.Favorites,
                PageRoute = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SIDE_PANE_FAVORITE),
                Icon = new FontIcon() { Glyph = "\uE728" },
            },
            new()
            {
                Tag = ITEM_FILTER_PRESETS,
                Name = StringResource.FilterPresets,
                PageRoute = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SIDE_PANE_FILTER_PRESETS),
                Icon = new FontIcon() { Glyph = "\uE71C" },
            },
            new()
            {
                Tag = ITEM_FOLDERS,
                Name = StringResource.Folders,
                PageRoute = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SIDE_PANE_FOLDERS),
                Icon = new FontIcon() { Glyph = "\uE8B7" },
            },
            new()
            {
                Tag = ITEM_HISTORY,
                Name = StringResource.History,
                PageRoute = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SIDE_PANE_HISTORY),
                Icon = new FontIcon() { Glyph = "\uE81C" },
            },
        ];

        InitializeComponent();
    }

    //
    // Public Methods
    //

    public void Initialize(ISidePaneHandler handler)
    {
        _handler = handler;
    }

    public void RestoreLastStatus()
    {
        bool pinned = AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).GetValueOrDefault(KVNames.KV_KEY_APP_SIDE_PANE_PINNED, false);
        SetPinState(pinned);
    }

    public void SetPage(string itemTag)
    {
        if (!NavigateToItem(itemTag))
        {
            _initialTag = itemTag;
        }
    }

    //
    // Events
    //

    private void MainNavigationView_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        LoadSidebarItems();
    }

    private void OnNavPaneSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem viewItem || _handler is null)
        {
            return;
        }

        object? currentPage = ContentFrame.Content;
        if (currentPage is not null)
        {
            _pageCache[_currentTag] = currentPage;
        }

        if (viewItem.Tag is not string tag)
        {
            return;
        }

        if (tag == _currentTag)
        {
            return;
        }

        ContentFrame.Content = null;
        _currentTag = tag;
        AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).Set(KVNames.KV_KEY_APP_SIDE_PANE_LAST_ITEM, tag);
        if (_pageCache.TryGetValue(tag, out object? pageCache))
        {
            ContentFrame.Content = pageCache;
            return;
        }

        if (!_items.TryGetValue(tag, out SidebarPageItem? item))
        {
            return;
        }

        PageNavigationBundle bundle = AppRouter.Process(item.PageRoute)!;
        _handler.TransferAbility(bundle);
        ContentFrame.Navigate(bundle.PageTrait.GetPageType(), bundle);
    }

    private void PinButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        bool pinned = !Pinned;
        SetPinState(pinned);
        AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).Set(KVNames.KV_KEY_APP_SIDE_PANE_PINNED, pinned);
    }

    //
    // Helpers
    //

    private void LoadSidebarItems()
    {
        List<SidebarPageItem> allPageItems = [.. _internalItems];
        foreach (PluginContext plugin in PluginManager.Instance.GetActivePlugins())
        {
            foreach (ISidebarPageProvider provider in plugin.GetAllSidebarPageProviders())
            {
                SidebarPageItem item = new()
                {
                    Tag = $"Plugin_{provider.Host}",
                    Name = provider.Name,
                    PageRoute = Route.Create(RouterConstants.SCHEME_APP + provider.Host),
                    Icon = provider.Icon,
                };
                allPageItems.Add(item);
            }
        }

        List<SidebarPageItem> actualPageItems = [];
        _items.Clear();
        foreach (SidebarPageItem item in allPageItems)
        {
            if (_items.TryAdd(item.Tag, item))
            {
                actualPageItems.Add(item);
            }
        }

        MainNavigationView.MenuItems.Clear();
        foreach (SidebarPageItem pageItem in actualPageItems)
        {
            NavigationViewItem item = new()
            {
                Icon = pageItem.Icon,
                Tag = pageItem.Tag,
            };
            ToolTipService.SetToolTip(item, pageItem.Name);
            MainNavigationView.MenuItems.Add(item);
        }

        string initialTag = _initialTag;
        if (string.IsNullOrEmpty(initialTag))
        {
            initialTag = AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).GetValueOrDefault(KVNames.KV_KEY_APP_SIDE_PANE_LAST_ITEM, string.Empty);
        }

        if (string.IsNullOrEmpty(initialTag) || !NavigateToItem(initialTag))
        {
            NavigateToItem(_internalItems[0].Tag);
        }
    }

    private bool NavigateToItem(string itemTag)
    {
        foreach (object item in MainNavigationView.MenuItems)
        {
            if (item is NavigationViewItem viewItem && viewItem.Tag is string tag && itemTag == tag)
            {
                MainNavigationView.SelectedItem = viewItem;
                return true;
            }
        }

        return false;
    }

    private void SetPinState(bool pinned)
    {
        Pinned = pinned;
        ViewModel.UpdatePinButton(pinned);
        PinStateChanged?.Invoke(this, pinned);
    }

    //
    // Types
    //

    public interface ISidePaneHandler
    {
        int GetWindowId();

        void TransferAbility(PageNavigationBundle bundle);
    }

    private class SidebarPageItem
    {
        public required string Tag { get; init; }
        public required string Name { get; init; }
        public required Route PageRoute { get; init; }
        public required IconElement Icon { get; init; }
    }
}
