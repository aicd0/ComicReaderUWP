// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.Plugins;
using ComicReaderUWP.Converters;
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

    private readonly List<SidebarPageItem> _builtinItems;

    private IHandler? _handler = null;
    private bool _isContainerReady = false;
    private string _initialPageTag = string.Empty;

    private readonly Dictionary<string, SidebarPageItem> _items = [];
    private readonly Dictionary<string, object> _pageCache = [];
    private string _currentPageTag = string.Empty;

    public bool IsPinned { get; private set; } = false;

    public SidebarView()
    {
        _builtinItems =
        [
            new()
            {
                Tag = ITEM_COMIC_INFO,
                Name = StringResource.ComicInfo,
                PageRoute = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SIDE_PANE_COMIC_INFO),
                Icon = new FontIconSource() { Glyph = "\uE946" },
            },
            new()
            {
                Tag = ITEM_PLAYLIST,
                Name = StringResource.ReadingList,
                PageRoute = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SIDE_PANE_PLAYLIST),
                Icon = new FontIconSource() { Glyph = "\uE7BC" },
            },
            new()
            {
                Tag = ITEM_TAGS,
                Name = StringResource.Tags,
                PageRoute = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SIDE_PANE_TAGS),
                Icon = new FontIconSource() { Glyph = "\uE8EC" },
            },
            new()
            {
                Tag = ITEM_FAVORITES,
                Name = StringResource.Favorites,
                PageRoute = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SIDE_PANE_FAVORITE),
                Icon = new FontIconSource() { Glyph = "\uE728" },
            },
            new()
            {
                Tag = ITEM_FILTER_PRESETS,
                Name = StringResource.FilterPresets,
                PageRoute = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SIDE_PANE_FILTER_PRESETS),
                Icon = new FontIconSource() { Glyph = "\uE71C" },
            },
            new()
            {
                Tag = ITEM_FOLDERS,
                Name = StringResource.Folders,
                PageRoute = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SIDE_PANE_FOLDERS),
                Icon = new FontIconSource() { Glyph = "\uE8B7" },
            },
            new()
            {
                Tag = ITEM_HISTORY,
                Name = StringResource.History,
                PageRoute = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SIDE_PANE_HISTORY),
                Icon = new FontIconSource() { Glyph = "\uE81C" },
            },
        ];

        InitializeComponent();
        SetPinState(false);
    }

    //
    // Public Methods
    //

    public void SetHandler(IHandler handler)
    {
        _handler = handler;
    }

    public void SetPage(string tag)
    {
        if (!SelectItem(tag))
        {
            _initialPageTag = tag;
        }
    }

    public SidebarStateJsonModel GetState()
    {
        return new()
        {
            IsPinned = IsPinned,
            SelectedItem = _currentPageTag,
        };
    }

    public void EnsureInitialContent()
    {
        _isContainerReady = true;
        UpdatePage();
    }

    public void RestoreState(SidebarStateJsonModel state)
    {
        _initialPageTag = state.SelectedItem ?? string.Empty;
        SetPinState(state.IsPinned);
    }

    //
    // Events
    //

    private void MainNavigationView_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        LoadItems();
    }

    private void MainNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        UpdatePage();
    }

    private void PinButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        bool pinned = !IsPinned;
        SetPinState(pinned);
        _handler?.OnStateChanged();
    }

    //
    // Helpers
    //

    private void LoadItems()
    {
        List<SidebarPageItem> allPageItems = [.. _builtinItems];
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
                Icon = IconSourceToIconElementConverter.Convert(pageItem.Icon),
                Tag = pageItem.Tag,
            };
            ToolTipService.SetToolTip(item, pageItem.Name);
            MainNavigationView.MenuItems.Add(item);
        }

        string initialTag = _initialPageTag;
        if (string.IsNullOrEmpty(initialTag) || !SelectItem(initialTag))
        {
            SelectItem(_builtinItems[0].Tag);
        }
    }

    private void UpdatePage()
    {
        if (_handler is null || !_isContainerReady)
        {
            return;
        }

        if (MainNavigationView.SelectedItem is not NavigationViewItem viewItem || viewItem.Tag is not string selectedTag)
        {
            return;
        }

        object? currentPage = ContentFrame.Content;
        if (currentPage is not null)
        {
            _pageCache[_currentPageTag] = currentPage;
        }

        if (selectedTag == _currentPageTag)
        {
            return;
        }

        ContentFrame.Content = null;
        _currentPageTag = selectedTag;
        _handler.OnStateChanged();

        if (_pageCache.TryGetValue(selectedTag, out object? pageCache))
        {
            ContentFrame.Content = pageCache;
            return;
        }

        if (!_items.TryGetValue(selectedTag, out SidebarPageItem? item))
        {
            return;
        }

        PageNavigationBundle bundle = AppRouter.Process(item.PageRoute)!;
        _handler.TransferAbility(bundle);
        ContentFrame.Navigate(bundle.PageTrait.GetPageType(), bundle);
    }

    private bool SelectItem(string tag)
    {
        foreach (object item in MainNavigationView.MenuItems)
        {
            if (item is NavigationViewItem viewItem && viewItem.Tag is string itemTag && itemTag == tag)
            {
                MainNavigationView.SelectedItem = viewItem;
                return true;
            }
        }

        return false;
    }

    private void SetPinState(bool pinned)
    {
        IsPinned = pinned;
        ViewModel.UpdatePinButton(pinned);
        PinStateChanged?.Invoke(this, pinned);
    }

    //
    // Types
    //

    public interface IHandler
    {
        void TransferAbility(PageNavigationBundle bundle);

        void OnStateChanged();
    }

    private class SidebarPageItem
    {
        public required string Tag { get; init; }
        public required string Name { get; init; }
        public required Route PageRoute { get; init; }
        public required IconSource Icon { get; init; }
    }

    public class SidebarStateJsonModel
    {
        [JsonPropertyName("IsOpen")]
        public bool IsOpen { get; set; }

        [JsonPropertyName("IsPinned")]
        public bool IsPinned { get; set; }

        [JsonPropertyName("Width")]
        public double Width { get; set; }

        [JsonPropertyName("SelectedItem")]
        public string? SelectedItem { get; set; }
    }
}
