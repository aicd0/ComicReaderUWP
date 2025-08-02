// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.Common.BaseUI;
using ComicReader.Views.DevTools;
using ComicReader.Views.Main;
using ComicReader.Views.Navigation;
using ComicReader.Views.SidePane.Favorite;
using ComicReader.Views.SidePane.History;
using ComicReader.Views.SidePane.Tags;

namespace ComicReader.Helpers.Navigation;

internal class OpenPageInterceptor : IRouterInterceptor
{
    public NavigationBundle Intercept(Route route)
    {
        IPageTrait pageTrait = route.Host switch
        {
            RouterConstants.HOST_MAIN => new DefaultPageTrait(typeof(MainPage)),
            RouterConstants.HOST_READER => ReaderPageTrait.Instance,
            RouterConstants.HOST_HOME => HomePageTrait.Instance,
            RouterConstants.HOST_SEARCH => SearchPageTrait.Instance,
            RouterConstants.HOST_SETTING => SettingPageTrait.Instance,
            RouterConstants.HOST_SIDE_PANE_FAVORITE => new DefaultPageTrait(typeof(FavoritePage)),
            RouterConstants.HOST_SIDE_PANE_HISTORY => new DefaultPageTrait(typeof(HistoryPage)),
            RouterConstants.HOST_SIDE_PANE_TAGS => new DefaultPageTrait(typeof(TagsPage)),
            RouterConstants.HOST_NAVIGATION => new DefaultPageTrait(typeof(NavigationPage)),
            RouterConstants.HOST_DEV_TOOLS => new DefaultPageTrait(typeof(DevToolsPage)),
            _ => throw new ArgumentException("Unknown host " + route.Host),
        };
        return new NavigationBundle(pageTrait, route.Queries, route.Url);
    }
}
