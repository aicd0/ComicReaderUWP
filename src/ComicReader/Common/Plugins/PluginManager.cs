// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

using ComicReader.Common.Constants;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Lifecycle;
using ComicReader.SDK.Common.Storage;
using ComicReader.SDK.Common.Utils;
using ComicReader.SDK.Database.KV;
using ComicReader.SDK.Plugins;

namespace ComicReader.Common.Plugins;

internal class PluginManager
{
    private const string TAG = nameof(PluginManager);
    private const string KEY_DISABLED_PLUGINS = "DisabledPlugins";

    public readonly static PluginManager Instance = new();

    public static string PluginsFolderPath => Path.Combine(StorageLocation.LocalFolderPath, "plugins");

    private static readonly MutableLiveData<bool> _pluginsChanged = new(false);
    public static ILiveData<bool> PluginsChanged => _pluginsChanged;

    private int _pluginLoaded = 0;
    private volatile bool _pluginInitialized = false;
    private readonly Dictionary<string, PluginContext> _plugins = [];
    private readonly ConcurrentDictionary<string, bool> _disabledPlugins = [];

    private PluginManager() { }

    public void LoadPlugins()
    {
        if (Interlocked.Exchange(ref _pluginLoaded, 1) == 1)
        {
            return;
        }

        LoadDisabledPlugins();
        string pluginsDir = PluginsFolderPath;
        Directory.CreateDirectory(pluginsDir);
        string[] pluginFiles = Directory.GetFiles(pluginsDir, "*.dll");
        foreach (string pluginFile in pluginFiles)
        {
            List<IPlugin> plugins = LoadAssembly(pluginFile);
            if (plugins.Count == 0)
            {
                Logger.E(TAG, $"Failed to load assembly '{Path.GetFileName(pluginFile)}'");
                continue;
            }

            Logger.I(TAG, $"Loaded assembly '{pluginFile}'");
            foreach (IPlugin plugin in plugins)
            {
                string name = plugin.Name;
                if (_plugins.ContainsKey(name))
                {
                    Logger.E(TAG, $"Duplicated plugin name: '{name}'");
                    continue;
                }

                PluginContext context = new(plugin, pluginFile);
                _plugins.Add(name, context);
                if (_disabledPlugins.ContainsKey(name))
                {
                    continue;
                }

                context.Initialize();
                Logger.I(TAG, $"Loaded plugin '{name}'");
            }
        }

        _pluginInitialized = true;
        SaveDisabledPlugins();
        NotifyPluginsChanged();
    }

    public PluginContext? GetPlugin(string pluginName)
    {
        if (!_pluginInitialized)
        {
            return null;
        }

        _plugins.TryGetValue(pluginName, out PluginContext? context);
        return context;
    }

    public IEnumerable<PluginContext> GetAllPlugins()
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

    public IEnumerable<PluginContext> GetActivePlugins()
    {
        if (!_pluginInitialized)
        {
            yield break;
        }

        foreach (PluginContext context in _plugins.Values)
        {
            if (context.Status != PluginStatusEnum.Initialized)
            {
                continue;
            }

            yield return context;
        }
    }

    public bool IsPluginEnabled(string pluginName)
    {
        if (!_pluginInitialized)
        {
            return false;
        }

        return !_disabledPlugins.ContainsKey(pluginName);
    }

    public void SetPluginEnabled(string pluginName, bool enabled)
    {
        if (enabled)
        {
            _disabledPlugins.TryRemove(pluginName, out _);
        }
        else
        {
            _disabledPlugins[pluginName] = true;
        }

        SaveDisabledPlugins();
        NotifyPluginsChanged();
    }

    private void LoadDisabledPlugins()
    {
        _disabledPlugins.Clear();
        string json = KVStore.App.GetCollection(DatabaseEntry.KV_LIB_PLUGINS).GetValueOrDefault(KEY_DISABLED_PLUGINS, "[]");
        IEnumerable<string>? disabledPlugins;
        try
        {
            disabledPlugins = System.Text.Json.JsonSerializer.Deserialize<IEnumerable<string>>(json);
        }
        catch (Exception e)
        {
            Logger.E(TAG, e);
            return;
        }

        if (disabledPlugins is null)
        {
            return;
        }

        foreach (string pluginName in disabledPlugins)
        {
            _disabledPlugins[pluginName] = true;
        }
    }

    private void SaveDisabledPlugins()
    {
        if (!_pluginInitialized)
        {
            return;
        }

        foreach (string pluginName in _disabledPlugins.Keys)
        {
            if (!_plugins.ContainsKey(pluginName))
            {
                _disabledPlugins.TryRemove(pluginName, out _);
            }
        }

        foreach (KeyValuePair<string, PluginContext> kvp in _plugins)
        {
            if (kvp.Value.Status == PluginStatusEnum.Error)
            {
                _disabledPlugins[kvp.Key] = true;
            }
        }

        List<string> disabledPlugins = [.. _disabledPlugins.Keys];
        disabledPlugins.Sort();
        string json = System.Text.Json.JsonSerializer.Serialize(disabledPlugins);
        KVStore.App.GetCollection(DatabaseEntry.KV_LIB_PLUGINS).Set(KEY_DISABLED_PLUGINS, json);
    }

    private static List<IPlugin> LoadAssembly(string pluginFile)
    {
        Assembly assembly;
        try
        {
            assembly = Assembly.LoadFrom(pluginFile);
        }
        catch (Exception e)
        {
            Logger.E(TAG, e);
            return [];
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
            return [];
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
                continue;
            }

            plugins.Add(plugin);
        }

        return plugins;
    }

    private static void NotifyPluginsChanged()
    {
        _pluginsChanged.Emit(true);
    }
}
