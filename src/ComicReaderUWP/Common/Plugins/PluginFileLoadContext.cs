// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReaderUWP.SDK.Plugins;

using Microsoft.UI.Xaml.Markup;

namespace ComicReaderUWP.Common.Plugins;

internal class PluginFileLoadContext
{
    public required PluginAssemblyLoader AssemblyLoader { get; init; }
    public IReadOnlyCollection<IPlugin> Plugins { get; init; } = [];
    public IReadOnlyCollection<IXamlMetadataProvider> XamlMetadataProviders { get; init; } = [];
    public string ResourceFolderPath { get; init; } = string.Empty;
    public required IReadOnlyDictionary<string, string> Assemblies { get; init; }
}
