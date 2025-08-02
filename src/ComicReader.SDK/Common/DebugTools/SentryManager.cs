// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.AppEnvironment;
using ComicReader.SDK.Common.Storage;

namespace ComicReader.SDK.Common.DebugTools;

public static class SentryManager
{
    private const string TAG_USER_LEVEL = "user-level";
    private const string LEVEL_INFO = "info";
    private const string LEVEL_WARNING = "warning";
    private const string LEVEL_ERROR = "error";

    private static volatile bool _initialized = false;

    public static void Initialize(string dsn, IReadOnlyDictionary<string, string> tags)
    {
        if (!DebugUtils.SentryEnabled || _initialized)
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
            o.Release = EnvironmentProvider.GetVersionName();

            foreach (KeyValuePair<string, string> tag in tags)
            {
                o.DefaultTags[tag.Key] = tag.Value;
            }
        });

        _initialized = true;

        //CaptureInfo("SentryInit");
    }

    internal static void CaptureInfo(string message)
    {
        if (!_initialized)
        {
            return;
        }

        using IDisposable scope = SentrySdk.PushScope();
        SentrySdk.SetTag(TAG_USER_LEVEL, LEVEL_INFO);
        SentrySdk.CaptureMessage(message);
    }

    internal static void CaptureWarning(Exception exception)
    {
        if (!_initialized)
        {
            return;
        }

        using IDisposable scope = SentrySdk.PushScope();
        SentrySdk.SetTag(TAG_USER_LEVEL, LEVEL_WARNING);
        SentrySdk.CaptureException(exception);
    }

    internal static void CaptureError(Exception exception)
    {
        if (!_initialized)
        {
            return;
        }

        using IDisposable scope = SentrySdk.PushScope();
        SentrySdk.SetTag(TAG_USER_LEVEL, LEVEL_ERROR);
        SentrySdk.CaptureException(exception);
    }
}
