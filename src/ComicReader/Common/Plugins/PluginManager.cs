// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Storage;
using ComicReader.SDK.Plugins;

namespace ComicReader.Common.Plugins;

internal class PluginManager
{
    private const string TAG = nameof(PluginManager);

    public readonly static PluginManager Instance = new();

    private int _pluginLoaded = 0;
    private int _pluginInitialized = 0;
    private readonly List<IPlugin> _plugins = [];

    private PluginManager() { }

    public void LoadPlugins()
    {
        if (Interlocked.Exchange(ref _pluginLoaded, 1) == 1)
        {
            return;
        }

        string pluginDir = Path.Combine(StorageLocation.LocalFolderPath, "plugins");
        Directory.CreateDirectory(pluginDir);
        string[] pluginFiles = Directory.GetFiles(pluginDir, "*.dll");
        foreach (string pluginFile in pluginFiles)
        {
            if (LoadPlugin(pluginFile))
            {
                Logger.I(TAG, $"Loaded plugin '{Path.GetFileName(pluginFile)}'");
            }
            else
            {
                Logger.E(TAG, $"Failed to load plugin '{Path.GetFileName(pluginFile)}'");
            }
        }

        foreach (IPlugin plugin in _plugins)
        {
            plugin.Initialize();
        }

        Interlocked.Exchange(ref _pluginInitialized, 1);
    }

    private bool LoadPlugin(string pluginFile)
    {
        Assembly assembly;
        try
        {
            assembly = Assembly.LoadFrom(pluginFile);
        }
        catch (Exception e)
        {
            Logger.E(TAG, e);
            return false;
        }

        IEnumerable<Type> pluginTypes = assembly
            .GetTypes()
            .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract);
        List<IPlugin> plugins = [];
        foreach (Type pluginType in pluginTypes)
        {
            IPlugin plugin;
            try
            {
                plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
            }
            catch (Exception e)
            {
                Logger.E(TAG, e);
                return false;
            }

            plugins.Add(plugin);
        }

        foreach (IPlugin plugin in plugins)
        {
            _plugins.Add(plugin);
        }

        return true;
    }
}
