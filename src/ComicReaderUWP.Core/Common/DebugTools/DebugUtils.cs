// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using ComicReaderUWP.Core.Common.Constants;
using ComicReaderUWP.Core.Common.Native;
using ComicReaderUWP.Core.Common.ServiceManagement;
using ComicReaderUWP.Core.Common.ServiceManagement.Services;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Core.Database.Misc;

namespace ComicReaderUWP.Core.Common.DebugTools;

public static class DebugUtils
{
    private const string TAG = nameof(DebugUtils);

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
                _debugMode = CoreDB.SdkKV.GetCollection(DatabaseEntry.KV_LIB_MAIN).GetValueOrDefault(DatabaseEntry.KV_KEY_MAIN_DEBUG_MODE, DebugBuild);
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
            CoreDB.SdkKV.GetCollection(DatabaseEntry.KV_LIB_MAIN).Set(DatabaseEntry.KV_KEY_MAIN_DEBUG_MODE, value);
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
                _developerMode = UnlockedDeveloperMode && CoreDB.SdkKV.GetCollection(DatabaseEntry.KV_LIB_MAIN).GetValueOrDefault(DatabaseEntry.KV_KEY_MAIN_DEVELOPER_MODE, true);
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
            CoreDB.SdkKV.GetCollection(DatabaseEntry.KV_LIB_MAIN).Set(DatabaseEntry.KV_KEY_MAIN_DEVELOPER_MODE, value);
        }
    }

    private static bool? _sentryEnabled = null;
    public static bool SentryEnabled
    {
        get
        {
            if (!_sentryEnabled.HasValue)
            {
                _sentryEnabled = !UnlockedDeveloperMode || CoreDB.SdkKV.GetCollection(DatabaseEntry.KV_LIB_MAIN).GetValueOrDefault(DatabaseEntry.KV_KEY_MAIN_SENTRY_ENABLED, true);
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
            CoreDB.SdkKV.GetCollection(DatabaseEntry.KV_LIB_MAIN).Set(DatabaseEntry.KV_KEY_MAIN_SENTRY_ENABLED, value);
        }
    }

    public static void ReportLastCrash()
    {
        CrashHandler.ReportLastCrash();
    }

    public static void CaptureFatalError(string message, Exception ex, bool fastFail = false)
    {
        IApplicationService? appService = ServiceManager.GetServiceNullable<IApplicationService>();

        // Handle launch crash
        if (appService?.Launching != false)
        {
            StringBuilder sb = new();
            sb.Append("A fatal error occurred during app launch. Press Ctrl+C to copy this message to clipboard:\n");
            sb.Append("Message:\n");
            sb.Append(message);
            sb.Append('\n');
            sb.Append("Exception stack trace:\n");
            sb.Append(ex.ToString());
            sb.Append('\n');
            sb.Append("Caller stack trace:\n");
            sb.Append(new System.Diagnostics.StackTrace(true).ToString());
            string text = sb.ToString();
            NativeMethods.MessageBoxW(nint.Zero, text, "Comic Reader UWP", 0x00000010);
        }

        Logger.E(TAG, message, ex);
        AppUnhandledException appException = new(message, ex);
        SentryManager.CaptureError(appException);
        CrashHandler.OnCrash(appException);

        if (fastFail)
        {
            Environment.FailFast(message, ex);
        }
    }

    private class AppUnhandledException(string message, Exception ex) : Exception(message, ex) { }
}
