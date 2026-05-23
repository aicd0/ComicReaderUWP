// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.SDK.Plugins;

namespace ComicReaderUWP.Common.Plugins;

internal class PluginLogger(string pluginName) : ILogger
{
    private const string TAG = "Plugins";

    public void D(string tag, string? message)
    {
        Logger.D(LogTag.N(TAG, pluginName, tag), message);
    }

    public void I(string tag, string? message)
    {
        Logger.I(LogTag.N(TAG, pluginName, tag), message);
    }

    public void W(string tag, string? message)
    {
        Logger.W(LogTag.N(TAG, pluginName, tag), message);
    }

    public void E(string tag, string? message, Exception? exception)
    {
        Logger.E(LogTag.N(TAG, pluginName, tag), message, exception);
    }

    public void F(string tag, string? message, Exception? exception)
    {
        Logger.F(LogTag.N(TAG, pluginName, tag), message, exception);
    }
}
