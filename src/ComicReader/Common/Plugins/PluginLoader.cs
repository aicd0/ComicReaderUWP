// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Storage;
using ComicReader.SDK.Plugins;

namespace ComicReader.Common.Plugins;

internal static class PluginLoader
{
    private const string TAG = nameof(PluginLoader);

    public static PluginFileLoadResult? LoadPluginFile(string pluginFile)
    {
        string extension = Path.GetExtension(pluginFile).ToLowerInvariant();
        return extension switch
        {
            ".dll" => LoadDllPlugin(pluginFile),
            ".zip" => LoadZipPlugin(pluginFile),
            _ => null,
        };
    }

    private static PluginFileLoadResult? LoadZipPlugin(string pluginFile)
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
        catch (Exception e)
        {
            Logger.F(TAG, e);
            return null;
        }

        try
        {
            Directory.CreateDirectory(extractDir);
            System.IO.Compression.ZipFile.ExtractToDirectory(pluginFile, extractDir);
        }
        catch (Exception e)
        {
            Logger.F(TAG, e);
            return null;
        }

        string[] dllFiles = Directory.GetFiles(extractDir, "*.dll", SearchOption.TopDirectoryOnly);
        PluginFileLoadResult finalResult = new()
        {
            ResourceFolderPath = extractDir,
        };
        foreach (string dllFile in dllFiles)
        {
            PluginFileLoadResult? result = LoadDllPlugin(dllFile);
            if (result is null || result.Plugins.Count == 0)
            {
                Logger.E(TAG, $"Failed to load assembly '{Path.GetFileName(dllFile)}' from zip plugin '{pluginFileName}'");
                continue;
            }

            finalResult.Plugins.AddRange(result.Plugins);
        }

        return finalResult;
    }

    private static PluginFileLoadResult? LoadDllPlugin(string pluginFile)
    {
        Assembly assembly;
        try
        {
            assembly = Assembly.LoadFrom(pluginFile);
        }
        catch (Exception e)
        {
            Logger.E(TAG, e);
            return null;
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
            return null;
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

        return new()
        {
            Plugins = plugins,
        };
    }

    public class PluginFileLoadResult
    {
        public List<IPlugin> Plugins { get; init; } = [];
        public string ResourceFolderPath { get; init; } = string.Empty;
    }
}
