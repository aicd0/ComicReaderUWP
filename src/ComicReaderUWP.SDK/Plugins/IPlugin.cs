// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Plugins;

public interface IPlugin
{
    string Name { get; }

    string Publisher { get; }

    int MajorVersion { get; }

    int MinorVersion { get; }

    void Initialize(IPluginContext context);
}
