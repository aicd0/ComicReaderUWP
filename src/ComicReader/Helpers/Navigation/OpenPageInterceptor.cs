// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.BaseUI;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.Views.Pages.DevTools;
using ComicReader.Views.Pages.Main;
using ComicReader.Views.Pages.Navigation;
using ComicReader.Views.Pages.SidePane.Favorite;
using ComicReader.Views.Pages.SidePane.History;
using ComicReader.Views.Pages.SidePane.Tags;

namespace ComicReader.Helpers.Navigation;

internal class OpenPageInterceptor : IRouterInterceptor
{
    public bool Intercept(Route route, out NavigationBundle? navigationBundle)
    {
        IPageTrait? pageTrait = route.Host switch
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
            RouterConstants.HOST_DEV_TOOLS => DebugUtils.DeveloperMode ? new DefaultPageTrait(typeof(DevToolsPage)) : null,
            _ => null,
        };

        if (pageTrait == null)
        {
            Logger.F(nameof(OpenPageInterceptor), $"Unknown host {route.Host}");
            navigationBundle = null;
            return false;
        }

        navigationBundle = new NavigationBundle(pageTrait, route.Queries, route.Url);
        return true;
    }
}
