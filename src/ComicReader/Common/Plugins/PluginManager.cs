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
    private volatile bool _pluginInitialized = false;
    private readonly Dictionary<string, PluginContext> _plugins = [];

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
                Logger.I(TAG, $"Loaded assembly '{Path.GetFileName(pluginFile)}'");
            }
            else
            {
                Logger.E(TAG, $"Failed to load assembly '{Path.GetFileName(pluginFile)}'");
            }
        }

        _pluginInitialized = true;
    }

    public IEnumerable<PluginContext> GetAllPluginContext()
    {
        if (!_pluginInitialized)
        {
            yield break;
        }

        foreach (PluginContext context in _plugins.Values)
        {
            yield return context;
        }
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

        IEnumerable<Type> pluginTypes;
        try
        {
            pluginTypes = assembly
                .GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract);
        }
        catch (Exception e)
        {
            Logger.E(TAG, e);
            return false;
        }

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
            string name = plugin.Name;
            if (_plugins.ContainsKey(name))
            {
                throw new InvalidOperationException($"Duplicated plugin name: '{name}'");
            }

            PluginContext context = new(plugin);
            _plugins.Add(name, context);
            plugin.Initialize(context);
            Logger.I(TAG, $"Loaded plugin '{name}'");
        }

        return true;
    }
}
