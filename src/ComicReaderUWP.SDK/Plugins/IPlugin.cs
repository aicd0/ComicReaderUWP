// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.SDK.Plugins;

public interface IPlugin
{
    string Name { get; }

    string Publisher { get; }

    string Description { get; }

    IconSource? Icon { get; }

    string Version { get; }

    void Initialize(IPluginContext context);
}
