// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Storage;
using ComicReaderUWP.SDK.Plugins;

using Microsoft.UI.Xaml.Markup;

namespace ComicReaderUWP.Common.Plugins;

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

        string[] depFiles = Directory.GetFiles(extractDir, "*.deps.json", SearchOption.TopDirectoryOnly);
        if (depFiles.Length != 1)
        {
            Logger.E(TAG, $"Expected exactly one .deps.json file in plugin '{pluginFile}', but found {depFiles.Length}");
            return null;
        }

        string dllFile = depFiles[0][..^10] + ".dll";
        if (!File.Exists(dllFile))
        {
            string dllFileName = Path.GetFileName(dllFile);
            Logger.E(TAG, $"Main assembly '{dllFileName}' not found in plugin '{pluginFile}'");
            return null;
        }

        PluginFileLoadResult? loadDllResult = LoadDllPlugin(dllFile);
        if (loadDllResult is null)
        {
            return null;
        }

        return new()
        {
            LoadContext = loadDllResult.LoadContext,
            Plugins = loadDllResult.Plugins,
            XamlMetadataProviders = loadDllResult.XamlMetadataProviders,
            ResourceFolderPath = extractDir,
        };
    }

    private static PluginFileLoadResult? LoadDllPlugin(string pluginFile)
    {
        PluginLoadContext loadContext = new(pluginFile);
        Assembly assembly;
        try
        {
            assembly = loadContext.LoadFromAssemblyPath(pluginFile);
        }
        catch (Exception e)
        {
            Logger.E(TAG, e);
            return null;
        }

        List<IPlugin> plugins = CreateInstancesFromAssembly<IPlugin>(assembly);
        List<IXamlMetadataProvider> xamlMetadataProviders = CreateInstancesFromAssembly<IXamlMetadataProvider>(assembly);
        return new()
        {
            LoadContext = loadContext,
            Plugins = plugins,
            XamlMetadataProviders = xamlMetadataProviders,
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
        catch (Exception e)
        {
            Logger.E(TAG, e);
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
            catch (Exception e)
            {
                Logger.E(TAG, e);
                continue;
            }

            instances.Add(instance);
        }

        return instances;
    }

    public class PluginFileLoadResult
    {
        public required PluginLoadContext LoadContext { get; init; }
        public List<IPlugin> Plugins { get; init; } = [];
        public List<IXamlMetadataProvider> XamlMetadataProviders { get; init; } = [];
        public string ResourceFolderPath { get; init; } = string.Empty;
    }
}
