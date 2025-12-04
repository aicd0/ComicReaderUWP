// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.Common.InitTask;

internal static class LaunchPerformanceTracker
{
    private const string TAG = nameof(LaunchPerformanceTracker);
    private static long _appEntryTime = 0;

    public static void MarkAppEntry()
    {
        _appEntryTime = GetTick();
    }

    public static void MarkAppLaunched()
    {
        LogStage("AppLaunched");
    }

    public static void MarkTabRestored()
    {
        LogStage("TabRestored");
    }

    private static void LogStage(string stageName)
    {
        long timeUsed = GetTick() - _appEntryTime;
        Logger.D(TAG, $"{stageName}={timeUsed}ms");
    }

    private static long GetTick()
    {
        return Environment.TickCount64;
    }
}
