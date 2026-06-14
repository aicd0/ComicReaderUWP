// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.Plugins;

namespace ComicReaderUWP.Helpers.Navigation;

internal class PageNavigationBundle : IPageNavigationBundle, SDK.Plugins.UI.IPageNavigationBundle
{
    public required IPageTrait PageTrait { get; init; }
    public PageBundle Bundle { get; private set; }
    public string Url { get; private set; }
    public PageCommunicator Communicator { get; } = new();

    public PageNavigationBundle(Route route)
    {
        Bundle = new PageBundle(route.Queries);
        Url = route.Url;
        _windowContext = new(() => PluginWindowContext.From(Communicator));
    }

    public void SetUrl(string url)
    {
        var route = Route.Create(url);
        Url = route.Url;
        Bundle = new PageBundle(route.Queries);
    }

    //
    // SDK.Plugins.UI.IPageNavigationBundle Implementation
    //

    private readonly Lazy<SDK.Plugins.UI.IWindowContext> _windowContext;

    SDK.Plugins.UI.IWindowContext SDK.Plugins.UI.IPageNavigationBundle.WindowContext => _windowContext.Value;
}
