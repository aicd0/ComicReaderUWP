// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Text;

using ComicReader.SDK.Common.AppEnvironment;
using ComicReader.SDK.Common.ServiceManagement;

using Windows.Storage;

namespace ComicReader.Common.Services;

internal class ApplicationService : IApplicationService
{
#if PORTABLE
    private const bool PORTABLE = true;
#else
    private const bool PORTABLE = false;
#endif

    private const string DIR_USER = "user";

    private static string GetDeploymentPath()
    {
        return AppContext.BaseDirectory;
    }

#pragma warning disable CS0162 // Unreachable code detected
    private readonly Lazy<string> _localFolderPath = new(() =>
    {
        if (PORTABLE)
        {
            return Path.Combine(GetDeploymentPath(), DIR_USER, "local");
        }
        else
        {
            return ApplicationData.Current.LocalFolder.Path;
        }
    });

    private readonly Lazy<string> _localCacheFolderPath = new(() =>
    {
        if (PORTABLE)
        {
            return Path.Combine(GetDeploymentPath(), DIR_USER, "local_cache");
        }
        else
        {
            return ApplicationData.Current.LocalCacheFolder.Path;
        }
    });

    private readonly Lazy<string> _temporaryFolderPath = new(() =>
    {
        if (PORTABLE)
        {
            return Path.Combine(GetDeploymentPath(), DIR_USER, "temporary");
        }
        else
        {
            return ApplicationData.Current.TemporaryFolder.Path;
        }
    });
#pragma warning restore CS0162 // Unreachable code detected

    public bool IsPortableBuild()
    {
        return PORTABLE;
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
        StringBuilder sb = new();
        EnvironmentProvider.Instance.AppendDebugText(sb);
        return sb.ToString();
    }
}
