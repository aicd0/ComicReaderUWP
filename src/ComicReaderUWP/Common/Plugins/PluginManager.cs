// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Common.InitTask;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Storage;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.SDK.Plugins;

using Microsoft.UI.Xaml.Markup;

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
            PluginFileLoadContext? loadContext = LoadPluginFile(pluginFile);
            if (loadContext is null || loadContext.Plugins.Count == 0)
            {
                Logger.E(TAG, $"Failed to load assembly '{Path.GetFileName(pluginFile)}'");
                continue;
            }

            Logger.I(TAG, $"Loaded assembly '{pluginFile}'");
            List<PluginContext> plugins = [];
            foreach (IPlugin plugin in loadContext.Plugins)
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

                PluginContext pluginContext = new(plugin, pluginFile, loadContext);
                _plugins.Add(name, pluginContext);

                if (!InitTaskManager.Instance.SafeMode && !_disabledPlugins.ContainsKey(pluginContext.Name))
                {
                    plugins.Add(pluginContext);
                }
            }

            if (plugins.Count > 0)
            {
                foreach (PluginContext plugin in plugins)
                {
                    foreach (string assembly in plugin.Plugin.SharedAssemblies)
                    {
                        loadContext.AssemblyLoader.AddSharedAssemblyName(assembly);
                    }
                }

                List<IXamlMetadataProvider> xamlMetaProviders = [.. loadContext.XamlMetadataProviders];

                foreach (string assemblyName in loadContext.AssemblyLoader.SharedAssemblies)
                {
                    if (!loadContext.Assemblies.TryGetValue(assemblyName, out string? assemblyPath))
                    {
                        Logger.E(TAG, $"Failed to find shared assembly '{assemblyName}' for plugin '{pluginFile}'");
                        continue;
                    }

                    Assembly assembly;
                    try
                    {
                        assembly = Assembly.LoadFrom(assemblyPath);
                    }
                    catch (Exception ex)
                    {
                        Logger.E(TAG, $"Failed to load shared assembly '{assemblyName}' for plugin '{pluginFile}'", ex);
                        continue;
                    }

                    xamlMetaProviders.AddRange(CreateInstancesFromAssembly<IXamlMetadataProvider>(assembly));
                    Logger.I(TAG, $"Loaded shared assembly '{assemblyName}' for plugin '{pluginFile}'");
                }

                PluginXamlMetadataProvider.AddProviders(xamlMetaProviders);

                foreach (PluginContext plugin in plugins)
                {
                    plugin.Initialize();
                    Logger.I(TAG, $"Loaded plugin '{plugin.Name}'");
                }
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
        catch (Exception ex)
        {
            Logger.E(TAG, ex);
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

    private static PluginFileLoadContext? LoadPluginFile(string pluginFile)
    {
        string extension = Path.GetExtension(pluginFile).ToLowerInvariant();
        return extension switch
        {
            ".dll" => LoadDllPlugin(pluginFile),
            ".zip" => LoadZipPlugin(pluginFile),
            _ => null,
        };
    }

    private static PluginFileLoadContext? LoadZipPlugin(string pluginFile)
    {
        string pluginFileName = Path.GetFileNameWithoutExtension(pluginFile);
        string extractDir = Path.Combine(StorageLocation.TemporaryFolderPath, "plugins", pluginFileName);
        try
        {
            Directory.Delete(extractDir, true);
        }
        catch (DirectoryNotFoundException)
        {
            // Ignore
        }
        catch (Exception ex)
        {
            Logger.F(TAG, ex);
            return null;
        }

        try
        {
            Directory.CreateDirectory(extractDir);
            System.IO.Compression.ZipFile.ExtractToDirectory(pluginFile, extractDir);
        }
        catch (Exception ex)
        {
            Logger.F(TAG, ex);
            return null;
        }

        string[] depFiles = Directory.GetFiles(extractDir, "*.deps.json", SearchOption.TopDirectoryOnly);
        if (depFiles.Length != 1)
        {
            Logger.E(TAG, $"Expected exactly one .deps.json file in plugin '{pluginFile}', but found {depFiles.Length}");
            return null;
        }

        string[] dllFiles = Directory.GetFiles(extractDir, "*.dll", SearchOption.TopDirectoryOnly);
        Dictionary<string, string> assemblies = [];
        foreach (string dllFile in dllFiles)
        {
            string assemblyName = Path.GetFileNameWithoutExtension(dllFile);
            assemblies.Add(assemblyName, dllFile);
        }

        string mainAssemblyName = Path.GetFileName(depFiles[0])[..^10];
        if (!assemblies.TryGetValue(mainAssemblyName, out _))
        {
            Logger.E(TAG, $"Main assembly '{mainAssemblyName}' not found in plugin '{pluginFile}'");
            return null;
        }

        string mainAssemblyPath = assemblies[mainAssemblyName];
        PluginFileLoadContext? loadDllResult = LoadDllPlugin(mainAssemblyPath);
        if (loadDllResult is null)
        {
            return null;
        }

        return new()
        {
            AssemblyLoader = loadDllResult.AssemblyLoader,
            Plugins = loadDllResult.Plugins,
            XamlMetadataProviders = loadDllResult.XamlMetadataProviders,
            ResourceFolderPath = extractDir,
            Assemblies = assemblies,
        };
    }

    private static PluginFileLoadContext? LoadDllPlugin(string pluginFile)
    {
        PluginAssemblyLoader loadContext = new(pluginFile);
        Assembly assembly;
        try
        {
            assembly = loadContext.LoadFromAssemblyPath(pluginFile);
        }
        catch (Exception ex)
        {
            Logger.E(TAG, ex);
            return null;
        }

        List<IPlugin> plugins = CreateInstancesFromAssembly<IPlugin>(assembly);
        List<IXamlMetadataProvider> xamlMetadataProviders = CreateInstancesFromAssembly<IXamlMetadataProvider>(assembly);
        return new()
        {
            AssemblyLoader = loadContext,
            Plugins = plugins,
            XamlMetadataProviders = xamlMetadataProviders,
            Assemblies = new Dictionary<string, string> { { Path.GetFileNameWithoutExtension(pluginFile), pluginFile } },
        };
    }

    private static List<T> CreateInstancesFromAssembly<T>(Assembly assembly) where T : class
    {
        IEnumerable<Type> types;
        try
        {
            types = assembly
                .GetTypes()
                .Where(t => typeof(T).IsAssignableFrom(t) && !t.IsAbstract);
        }
        catch (Exception ex)
        {
            Logger.E(TAG, ex);
            return [];
        }

        List<T> instances = [];
        foreach (Type type in types)
        {
            T instance;
            try
            {
                instance = (T)Activator.CreateInstance(type)!;
            }
            catch (Exception ex)
            {
                Logger.E(TAG, ex);
                continue;
            }

            instances.Add(instance);
        }

        return instances;
    }

    [GeneratedRegex(@"^[a-zA-Z0-9_]+$")]
    private static partial Regex PluginNameRegex();
}
