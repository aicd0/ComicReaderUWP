// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Markup;

namespace ComicReaderUWP.SDK.Plugins;

/// <summary>
/// https://github.com/microsoft/microsoft-ui-xaml/issues/6299
/// </summary>
public partial class PluginXamlMetadataProvider : IXamlMetadataProvider
{
    private static List<IXamlMetadataProvider> _providers = [];
    private static bool _enteredGetXamlType1 = false;
    private static bool _enteredGetXamlType2 = false;
    private static bool _enteredGetXmlnsDefinitions = false;

    public static void AddProviders(IEnumerable<IXamlMetadataProvider> providers)
    {
        List<IXamlMetadataProvider> copy = [.. _providers];
        copy.AddRange(providers);
        _providers = copy;
    }

    IXamlType? IXamlMetadataProvider.GetXamlType(Type type)
    {
        if (_enteredGetXamlType1)
        {
            return null;
        }

        _enteredGetXamlType1 = true;
        try
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
        finally
        {
            _enteredGetXamlType1 = false;
        }
    }

    IXamlType? IXamlMetadataProvider.GetXamlType(string fullName)
    {
        if (_enteredGetXamlType2)
        {
            return null;
        }

        _enteredGetXamlType2 = true;
        try
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
        finally
        {
            _enteredGetXamlType2 = false;
        }
    }

    XmlnsDefinition[] IXamlMetadataProvider.GetXmlnsDefinitions()
    {
        if (_enteredGetXmlnsDefinitions)
        {
            return [];
        }

        _enteredGetXmlnsDefinitions = true;
        try
        {
            List<XmlnsDefinition> definitions = [];
            foreach (IXamlMetadataProvider provider in _providers)
            {
                definitions.AddRange(provider.GetXmlnsDefinitions());
            }

            return [.. definitions];
        }
        finally
        {
            _enteredGetXmlnsDefinitions = false;
        }
    }
}
