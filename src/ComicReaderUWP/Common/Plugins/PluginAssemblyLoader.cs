// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;

using ComicReaderUWP.Core.Common.DebugTools;

namespace ComicReaderUWP.Common.Plugins;

internal class PluginAssemblyLoader(string pluginPath) : AssemblyLoadContext(isCollectible: true)
{
    private const string TAG = nameof(PluginAssemblyLoader);

    private HashSet<string> DefaultContextAssemblies { get; } = [];
    private HashSet<string> PluginContextAssemblies { get; } = [];

    private readonly HashSet<string> _sharedAssemblies = [];
    public IReadOnlySet<string> SharedAssemblies => _sharedAssemblies;

    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

    public void AddSharedAssemblyName(string name)
    {
        if (DefaultContextAssemblies.Contains(name) || PluginContextAssemblies.Contains(name))
        {
            Logger.E(TAG, $"Failed to add shared assembly '{name}' because it is already loaded");
            return;
        }

        _sharedAssemblies.Add(name);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string? name = assemblyName.Name;
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        Assembly? assembly = LoadInternal(assemblyName);

        if (assembly is null)
        {
            DefaultContextAssemblies.Add(name);
        }
        else
        {
            PluginContextAssemblies.Add(name);
        }

        return assembly;
    }

    private Assembly? LoadInternal(AssemblyName assemblyName)
    {
        string? name = assemblyName.Name;
        if (!string.IsNullOrEmpty(name) && SharedAssemblies.Contains(name))
        {
            return null;
        }

        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath is not null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }
}
