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
    // Comic Edited Handlers
    //

    private IComicEditedHandler? _comicEditedHandlers = null;

    public void RegisterComicEditedHandler(IComicEditedHandler handler)
    {
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
}
