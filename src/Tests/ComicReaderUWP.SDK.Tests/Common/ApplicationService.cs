// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.SDK.Common.ServiceManagement;
using ComicReaderUWP.SDK.Plugins;

namespace ComicReaderUWP.SDK.Tests.Common;

internal class ApplicationService : IApplicationService
{
    private const string DIR_USER = "user";

    private static string GetDeploymentPath()
    {
        return AppContext.BaseDirectory;
    }

    private readonly Lazy<string> _localFolderPath = new(() =>
    {
        return Path.Combine(GetDeploymentPath(), DIR_USER, "local");
    });

    private readonly Lazy<string> _localCacheFolderPath = new(() =>
    {
        return Path.Combine(GetDeploymentPath(), DIR_USER, "local_cache");
    });

    private readonly Lazy<string> _temporaryFolderPath = new(() =>
    {
        return Path.Combine(GetDeploymentPath(), DIR_USER, "temporary");
    });

    public bool IsPortableBuild()
    {
        return true;
    }

    public string GetLocalFolderPath()
    {
        return _localFolderPath.Value;
    }

    public string GetLocalCacheFolderPath()
    {
        return _localCacheFolderPath.Value;
    }

    public string GetTemporaryFolderPath()
    {
        return _temporaryFolderPath.Value;
    }

    public string GetEnvironmentDebugInfo()
    {
        return string.Empty;
    }

    public bool IsShuttingDown()
    {
        return false;
    }

    public IEnumerable<IPlugin> GetLoadedPlugins()
    {
        yield break;
    }
}
