// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.SDK.Plugins;

namespace ComicReader.Common.Plugins;

internal class PluginContext(IPlugin plugin) : IPluginContext
{
    public IPlugin Plugin => plugin;

    private readonly string _pluginName = plugin.Name;

    //
    // Before Comic Updating Handlers
    //

    private IBeforeComicUpdatingHandler? _beforeComicUpdatingHandlers = null;

    public void RegisterBeforeComicUpdatingHandler(IBeforeComicUpdatingHandler handler)
    {
        if (_beforeComicUpdatingHandlers is not null)
        {
            throw new InvalidOperationException($"Already registered: '{_pluginName}'");
        }

        _beforeComicUpdatingHandlers = handler;
    }

    public void DispatchBeforeComicUpdatingEvent(IComicModel comic)
    {
        _beforeComicUpdatingHandlers?.OnComicUpdating(comic);
    }
}
