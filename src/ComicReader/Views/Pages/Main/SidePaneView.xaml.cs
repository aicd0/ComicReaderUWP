// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReader.Common.BaseUI;
using ComicReader.Common.Constants;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.Utils;
using ComicReader.SDK.Database.KV;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Pages.Main;

internal sealed partial class SidePaneView : BaseUserControl
{
    private const string FAVORITES = "Favorites";
    private const string HISTORY = "History";
    private const string TAGS = "Tags";
    private const string FILTER_PRESETS = "FilterPresets";

    public delegate void PinStateChangedEventHandler(SidePaneView sender, bool pinned);
    public event PinStateChangedEventHandler? PinStateChanged;

    private readonly SidePaneViewModel ViewModel = new();

    private ISidePaneHandler? _handler = null;
    private readonly Dictionary<string, object> _pageCache = [];
    private string _currentItem = string.Empty;

    public bool Pinned { get; private set; } = false;

    public SidePaneView()
    {
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
        string lastSidePaneItem = KVStore.Default.GetCollection(DatabaseEntry.KV_LIB_APP).GetValueOrDefault(DatabaseEntry.KV_KEY_APP_SIDE_PANE_LAST_ITEM, string.Empty);
        if (!NavigateToItem(lastSidePaneItem))
        {
            NavigateToItem(FAVORITES);
        }

        bool pinned = KVStore.Default.GetCollection(DatabaseEntry.KV_LIB_APP).GetValueOrDefault(DatabaseEntry.KV_KEY_APP_SIDE_PANE_PINNED, false);
        SetPinState(pinned);
    }

    //
    // Events
    //

    private void OnNavPaneSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem viewItem || _handler is null)
        {
            return;
        }

        object? currentPage = ContentFrame.Content;
        if (currentPage is not null)
        {
            _pageCache[_currentItem] = currentPage;
        }

        string item = viewItem.Name;
        if (item == _currentItem)
        {
            return;
        }

        ContentFrame.Content = null;
        _currentItem = item;
        if (_pageCache.TryGetValue(item, out object? pageCache))
        {
            ContentFrame.Content = pageCache;
            return;
        }

        Route route = item switch
        {
            FAVORITES => Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SIDE_PANE_FAVORITE),
            HISTORY => Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SIDE_PANE_HISTORY),
            TAGS => Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SIDE_PANE_TAGS),
            FILTER_PRESETS => Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SIDE_PANE_FILTER_PRESETS),
            _ => Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_SIDE_PANE_FAVORITE),
        };

        route.WithParam(RouterConstants.ARG_WINDOW_ID, _handler.GetWindowId().ToString());
        NavigationBundle bundle = AppRouter.Process(route)!;
        _handler.TransferAbility(bundle);
        ContentFrame.Navigate(bundle.PageTrait.GetPageType(), bundle);
        KVStore.Default.GetCollection(DatabaseEntry.KV_LIB_APP).Set(DatabaseEntry.KV_KEY_APP_SIDE_PANE_LAST_ITEM, item);
    }

    private void PinButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        bool pinned = !Pinned;
        SetPinState(pinned);
        KVStore.Default.GetCollection(DatabaseEntry.KV_LIB_APP).Set(DatabaseEntry.KV_KEY_APP_SIDE_PANE_PINNED, pinned);
    }

    //
    // Helpers
    //

    private bool NavigateToItem(string itemName)
    {
        foreach (object item in MainNavigationView.MenuItems)
        {
            if (item is NavigationViewItem viewItem && viewItem.Name == itemName)
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

        void TransferAbility(NavigationBundle bundle);
    }
}
