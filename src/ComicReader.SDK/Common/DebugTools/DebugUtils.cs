// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.Constants;
using ComicReader.SDK.Common.KVStorage;

namespace ComicReader.SDK.Common.DebugTools;

public static class DebugUtils
{
#if DEBUG
    private const bool IS_DEBUG_BUILD = true;
#else
    private const bool IS_DEBUG_BUILD = false;
#endif

    public static bool DebugBuild => IS_DEBUG_BUILD;

    private static bool? _debugMode = null;
    public static bool DebugMode
    {
        get
        {
            if (!_debugMode.HasValue)
            {
                _debugMode = KVDatabase.Sdk.GetBoolean(DatabaseEntry.KV_LIB_MAIN, DatabaseEntry.KV_KEY_MAIN_DEBUG_MODE, DebugBuild);
            }

            return _debugMode.Value;
        }
        set
        {
            if (value == _debugMode)
            {
                return;
            }

            _debugMode = value;
            KVDatabase.Sdk.SetBoolean(DatabaseEntry.KV_LIB_MAIN, DatabaseEntry.KV_KEY_MAIN_DEBUG_MODE, value);
        }
    }

    public static bool DebugModeStrict => IS_DEBUG_BUILD && DebugMode;

    private static bool UnlockedDeveloperMode => DebugBuild || DebugCommand.UnlockedDeveloperMode;

    private static bool? _developerMode = null;
    public static bool DeveloperMode
    {
        get
        {
            if (!_developerMode.HasValue)
            {
                _developerMode = UnlockedDeveloperMode && KVDatabase.Sdk.GetBoolean(DatabaseEntry.KV_LIB_MAIN, DatabaseEntry.KV_KEY_MAIN_DEVELOPER_MODE, true);
            }

            return _developerMode.Value;
        }
        set
        {
            if (value == _developerMode)
            {
                return;
            }

            _developerMode = value;
            KVDatabase.Sdk.SetBoolean(DatabaseEntry.KV_LIB_MAIN, DatabaseEntry.KV_KEY_MAIN_DEVELOPER_MODE, value);
        }
    }

    private static bool? _sentryEnabled = null;
    public static bool SentryEnabled
    {
        get
        {
            if (!_sentryEnabled.HasValue)
            {
                _sentryEnabled = !UnlockedDeveloperMode || KVDatabase.Sdk.GetBoolean(DatabaseEntry.KV_LIB_MAIN, DatabaseEntry.KV_KEY_MAIN_SENTRY_ENABLED, true);
            }

            return _sentryEnabled.Value;
        }
        set
        {
            if (value == _sentryEnabled)
            {
                return;
            }

            _sentryEnabled = value;
            KVDatabase.Sdk.SetBoolean(DatabaseEntry.KV_LIB_MAIN, DatabaseEntry.KV_KEY_MAIN_SENTRY_ENABLED, value);
        }
    }

    public static void Initialize()
    {
        DebugSwitchModel.Instance.Initialize();
    }

    public static void ReportLastCrash()
    {
        CrashHandler.ReportLastCrash();
    }

    public static void TrackError(Action action, bool fastFail = false)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            CaptureFatalErrorInternal(null, e, fastFail);
            throw;
        }
    }

    public static void CaptureFatalError(string message, Exception e, bool fastFail = false)
    {
        CaptureFatalErrorInternal(message, e, fastFail);
    }

    private static void CaptureFatalErrorInternal(string? message, Exception e, bool fastFail)
    {
        AppUnhandledException appException = new(message, e);
        SentryManager.CaptureError(appException);
        CrashHandler.OnUnhandledException(appException);

        if (fastFail)
        {
            Environment.FailFast(message, e);
        }
    }

    private class AppUnhandledException(string? message, Exception innerException) : Exception(message, innerException) { }
}
