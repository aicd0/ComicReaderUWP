// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Text;

using ComicReaderUWP.Common.InitTask;
using ComicReaderUWP.Core.Common.AppEnvironment;
using ComicReaderUWP.Core.Common.ServiceManagement.Services;

using Windows.Storage;

namespace ComicReaderUWP.Common.Services;

internal class ApplicationService : IApplicationService
{
    private const string TAG = nameof(ApplicationService);

#if PORTABLE
    private const bool PORTABLE = true;
#else
    private const bool PORTABLE = false;
#endif

    private const string DIR_USER = "user";

#pragma warning disable CS0162 // Unreachable code detected
    private static readonly Lazy<string> _localFolderPath = new(() =>
    {
        if (PORTABLE)
        {
            return Path.Combine(GetDeploymentPath(), DIR_USER, "Local");
        }
        else
        {
            return ApplicationData.Current.LocalFolder.Path;
        }
    });

    private static readonly Lazy<string> _localCacheFolderPath = new(() =>
    {
        if (PORTABLE)
        {
            return Path.Combine(GetDeploymentPath(), DIR_USER, "LocalCache");
        }
        else
        {
            return ApplicationData.Current.LocalCacheFolder.Path;
        }
    });

    private static readonly Lazy<string> _temporaryFolderPath = new(() =>
    {
        if (PORTABLE)
        {
            return Path.Combine(GetDeploymentPath(), DIR_USER, "Temporary");
        }
        else
        {
            return ApplicationData.Current.TemporaryFolder.Path;
        }
    });
#pragma warning restore CS0162 // Unreachable code detected

    private static bool _launching = true;
    private static bool _exiting = false;

    public static void StopLaunching()
    {
        _launching = false;
    }

    public static void StartExiting()
    {
        _launching = false;
        _exiting = true;
        App.Instance.WindowManager.LockWindowState();
    }

    private static string GetDeploymentPath()
    {
        return AppContext.BaseDirectory;
    }

    public bool PortableBuild => PORTABLE;

    public bool SafeMode => InitTaskManager.Instance.SafeMode;

    public bool Launching => _launching;

    public bool Exiting => _exiting;

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
