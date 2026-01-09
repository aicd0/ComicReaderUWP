// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.BaseUI;

namespace ComicReaderUWP.Helpers.Navigation;

internal class NavigationBundle(Route route) : INavigationBundle
{
    public required IPageTrait PageTrait { get; init; }
    public PageBundle Bundle { get; private set; } = new PageBundle(route.Queries);
    public string Url { get; private set; } = route.Url;
    public PageCommunicator Communicator { get; } = new();

    public void SetUrl(string url)
    {
        var route = Route.Create(url);
        Url = route.Url;
        Bundle = new PageBundle(route.Queries);
    }
}
