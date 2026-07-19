// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.AppEnvironment;
using ComicReaderUWP.Core.Common.ServiceManagement;
using ComicReaderUWP.Core.Common.ServiceManagement.Services;
using ComicReaderUWP.Core.Common.Storage;

namespace ComicReaderUWP.Core.Common.DebugTools;

public static class SentryManager
{
    private const string TAG_USER_LEVEL = "c-user-level";
    private const string TAG_LAUNCHING = "c-launching";
    private const string TAG_EXITING = "c-exiting";
    private const string LEVEL_INFO = "info";
    private const string LEVEL_WARNING = "warning";
    private const string LEVEL_ERROR = "error";

    private static volatile bool _initialized = false;

    private static bool Active => _initialized && ServiceManager.GetServiceNullable<IDebugService>()?.SentryEnabled != false;

    public static void Initialize(string dsn, IReadOnlyDictionary<string, string> tags)
    {
        if (_initialized)
        {
            return;
        }

        if (string.IsNullOrEmpty(dsn))
        {
            return;
        }

        SentrySdk.Init(o =>
        {
            o.AutoSessionTracking = true;
            o.CacheDirectoryPath = StorageLocation.LocalCacheFolderPath;
            o.Distribution = EnvironmentProvider.IsPortable() ? "portable" : "packaged";
            o.Dsn = dsn;
            o.Release = EnvironmentProvider.Instance.GetHostVersion();

            foreach (KeyValuePair<string, string> tag in tags)
            {
                o.DefaultTags[tag.Key] = tag.Value;
            }
        });

        _initialized = true;
    }

    internal static void CaptureInfo(string message)
    {
        if (!Active)
        {
            return;
        }

        using IDisposable scope = SentrySdk.PushScope();
        PushRuntimeTags();
        SentrySdk.SetTag(TAG_USER_LEVEL, LEVEL_INFO);
        SentrySdk.CaptureMessage(message);
    }

    internal static void CaptureWarning(Exception exception)
    {
        if (!Active)
        {
            return;
        }

        using IDisposable scope = SentrySdk.PushScope();
        PushRuntimeTags();
        SentrySdk.SetTag(TAG_USER_LEVEL, LEVEL_WARNING);
        SentrySdk.CaptureException(exception);
    }

    internal static void CaptureError(Exception exception)
    {
        if (!Active)
        {
            return;
        }

        using IDisposable scope = SentrySdk.PushScope();
        PushRuntimeTags();
        SentrySdk.SetTag(TAG_USER_LEVEL, LEVEL_ERROR);
        SentrySdk.CaptureException(exception);
    }

    private static void PushRuntimeTags()
    {
        SentrySdk.SetTag(TAG_LAUNCHING, ServiceManager.GetServiceNullable<IApplicationService>()?.Launching != false ? "true" : "false");
        SentrySdk.SetTag(TAG_EXITING, ServiceManager.GetServiceNullable<IApplicationService>()?.Exiting == true ? "true" : "false");
    }
}
