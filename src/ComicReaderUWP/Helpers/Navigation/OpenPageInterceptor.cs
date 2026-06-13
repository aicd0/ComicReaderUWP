// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Views.Pages.DevTools;
using ComicReaderUWP.Views.Pages.Main;
using ComicReaderUWP.Views.Pages.Sidebar.ComicInfo;
using ComicReaderUWP.Views.Pages.Sidebar.Favorite;
using ComicReaderUWP.Views.Pages.Sidebar.FilterPresets;
using ComicReaderUWP.Views.Pages.Sidebar.Folders;
using ComicReaderUWP.Views.Pages.Sidebar.History;
using ComicReaderUWP.Views.Pages.Sidebar.Playlist;
using ComicReaderUWP.Views.Pages.Sidebar.Tags;

namespace ComicReaderUWP.Helpers.Navigation;

internal class OpenPageInterceptor : IRouterInterceptor
{
    private readonly Dictionary<string, IPageTrait> _internalHosts = new()
    {
        { RouterConstants.HOST_MAIN, new DefaultPageTrait(typeof(MainPage)) },
        { RouterConstants.HOST_READER, ReaderPageTrait.Instance },
        { RouterConstants.HOST_HOME, HomePageTrait.Instance },
        { RouterConstants.HOST_SEARCH, SearchPageTrait.Instance },
        { RouterConstants.HOST_SETTINGS, SettingsPageTrait.Instance },
        { RouterConstants.HOST_SIDE_PANE_FAVORITE, new DefaultPageTrait(typeof(FavoritePage)) },
        { RouterConstants.HOST_SIDE_PANE_HISTORY, new DefaultPageTrait(typeof(HistoryPage)) },
        { RouterConstants.HOST_SIDE_PANE_TAGS, new DefaultPageTrait(typeof(TagsPage)) },
        { RouterConstants.HOST_SIDE_PANE_FOLDERS, new DefaultPageTrait(typeof(FoldersPage)) },
        { RouterConstants.HOST_SIDE_PANE_FILTER_PRESETS, new DefaultPageTrait(typeof(FilterPresetsPage)) },
        { RouterConstants.HOST_SIDE_PANE_PLAYLIST, new DefaultPageTrait(typeof(PlaylistPage)) },
        { RouterConstants.HOST_SIDE_PANE_COMIC_INFO, new DefaultPageTrait(typeof(ComicInfoPage)) },
        { RouterConstants.HOST_DEV_TOOLS, new DefaultPageTrait(typeof(DevToolsPage)) },
    };

    private readonly ConcurrentDictionary<string, IPageTrait> _hosts = [];

    public OpenPageInterceptor()
    {
        foreach (KeyValuePair<string, IPageTrait> item in _internalHosts)
        {
            _hosts[item.Key] = item.Value;
        }
    }

    public bool Intercept(Route route, out NavigationBundle? navigationBundle)
    {
        string host = route.Host;
        if (!_hosts.TryGetValue(host, out IPageTrait? pageTrait))
        {
            Logger.F(nameof(OpenPageInterceptor), $"Unknown host: '{host}'");
            navigationBundle = null;
            return false;
        }

        if (host == RouterConstants.HOST_DEV_TOOLS && !DebugUtils.DeveloperMode)
        {
            navigationBundle = null;
            return false;
        }

        navigationBundle = new NavigationBundle(route)
        {
            PageTrait = pageTrait,
        };
        return true;
    }

    public void RegisterPage(string host, IPageTrait pageTrait)
    {
        if (!_hosts.TryAdd(host, pageTrait))
        {
            throw new ArgumentException($"Host '{host}' is already presented.");
        }
    }
}
