// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Storage;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.SDK.Plugins;

namespace ComicReaderUWP.Common.Plugins;

internal partial class PluginManager
{
    private const string TAG = nameof(PluginManager);
    private const string KEY_DISABLED_PLUGINS = "DisabledPlugins";

    public static readonly PluginManager Instance = new();

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

        ReadDisabledPlugins();
        string pluginsDir = PluginsFolderPath;
        Directory.CreateDirectory(pluginsDir);
        string[] pluginFiles = Directory.GetFiles(pluginsDir);
        foreach (string pluginFile in pluginFiles)
        {
            PluginLoader.PluginFileLoadResult? result = PluginLoader.LoadPluginFile(pluginFile);
            if (result is null || result.Plugins.Count == 0)
            {
                Logger.E(TAG, $"Failed to load assembly '{Path.GetFileName(pluginFile)}'");
                continue;
            }

            Logger.I(TAG, $"Loaded assembly '{pluginFile}'");
            bool registeredXaml = false;
            foreach (IPlugin plugin in result.Plugins)
            {
                string name = plugin.Name;
                if (!PluginNameRegex().IsMatch(name))
                {
                    Logger.E(TAG, $"Invalid plugin name: '{name}'");
                    continue;
                }

                if (_plugins.ContainsKey(name))
                {
                    Logger.E(TAG, $"Duplicated plugin name: '{name}'");
                    continue;
                }

                PluginContext context = new(plugin, pluginFile, result);
                _plugins.Add(name, context);

                if (App.Instance.SafeMode || _disabledPlugins.ContainsKey(name))
                {
                    continue;
                }

                if (!registeredXaml)
                {
                    registeredXaml = true;
                    PluginXamlMetadataProvider.AddProviders(result.XamlMetadataProviders);
                }

                context.Initialize();
                Logger.I(TAG, $"Loaded plugin '{name}'");
            }
        }

        _pluginInitialized = true;
        WriteDisabledPlugins();
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

    public PluginContext? GetActivePlugin(string pluginName)
    {
        PluginContext? plugin = GetPlugin(pluginName);
        if (plugin is null || !plugin.IsActive)
        {
            return null;
        }

        return plugin;
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
            if (!context.IsActive)
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
        if (!_pluginInitialized)
        {
            return;
        }

        if (enabled)
        {
            _disabledPlugins.TryRemove(pluginName, out _);
        }
        else
        {
            _disabledPlugins[pluginName] = true;
        }

        WriteDisabledPlugins();
        NotifyPluginsChanged();
    }

    private void ReadDisabledPlugins()
    {
        _disabledPlugins.Clear();
        string json = AppDB.AppKV.GetCollection(KVNames.KV_LIB_PLUGINS).GetValueOrDefault(KEY_DISABLED_PLUGINS, "[]");
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

    private void WriteDisabledPlugins()
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
        AppDB.AppKV.GetCollection(KVNames.KV_LIB_PLUGINS).Set(KEY_DISABLED_PLUGINS, json);
    }

    private static void NotifyPluginsChanged()
    {
        _pluginsChanged.Emit(true);
    }

    [GeneratedRegex(@"^[a-zA-Z0-9_]+$")]
    private static partial Regex PluginNameRegex();
}
