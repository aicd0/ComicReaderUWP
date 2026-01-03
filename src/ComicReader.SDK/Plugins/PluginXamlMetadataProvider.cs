// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Markup;

namespace ComicReader.SDK.Plugins;

/// <summary>
/// https://github.com/microsoft/microsoft-ui-xaml/issues/6299
/// </summary>
public partial class PluginXamlMetadataProvider : IXamlMetadataProvider
{
    private static List<IXamlMetadataProvider> _providers = [];

    public static void AddProviders(IEnumerable<IXamlMetadataProvider> providers)
    {
        List<IXamlMetadataProvider> copy = [.. _providers];
        copy.AddRange(providers);
        _providers = copy;
    }

    IXamlType? IXamlMetadataProvider.GetXamlType(Type type)
    {
        foreach (IXamlMetadataProvider provider in _providers)
        {
            IXamlType xamlType = provider.GetXamlType(type);
            if (xamlType != null)
            {
                return xamlType;
            }
        }

        return null;
    }

    IXamlType? IXamlMetadataProvider.GetXamlType(string fullName)
    {
        foreach (IXamlMetadataProvider provider in _providers)
        {
            IXamlType xamlType = provider.GetXamlType(fullName);
            if (xamlType != null)
            {
                return xamlType;
            }
        }

        return null;
    }

    XmlnsDefinition[] IXamlMetadataProvider.GetXmlnsDefinitions()
    {
        List<XmlnsDefinition> definitions = [];
        foreach (IXamlMetadataProvider provider in _providers)
        {
            definitions.AddRange(provider.GetXmlnsDefinitions());
        }

        return [.. definitions];
    }
}
