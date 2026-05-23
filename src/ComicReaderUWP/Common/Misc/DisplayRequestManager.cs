// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.DebugTools;

using Windows.System.Display;

namespace ComicReaderUWP.Common.Misc;

internal static class DisplayRequestManager
{
    private const string TAG = nameof(DisplayRequestManager);

    private static readonly object _lock = new();
    private static DisplayRequest? _displayRequest;
    private static int _keepScreenOnCounter = 0;

    public static void IncrememtKeepScreenOn()
    {
        lock (_lock)
        {
            _keepScreenOnCounter++;
            if (_keepScreenOnCounter == 1)
            {
                DisplayRequest displayRequest = GetDisplayRequestNoLock();
                displayRequest.RequestActive();
                Logger.I(TAG, "RequestActive");
            }
        }
    }

    public static void DecrememtKeepScreenOn()
    {
        lock (_lock)
        {
            if (_keepScreenOnCounter == 0)
            {
                Logger.F(TAG, "Decrement operation must be preceded by an increment operation");
                return;
            }

            _keepScreenOnCounter--;
            if (_keepScreenOnCounter == 0)
            {
                DisplayRequest displayRequest = GetDisplayRequestNoLock();
                displayRequest.RequestRelease();
                Logger.I(TAG, "RequestRelease");
            }
        }
    }

    private static DisplayRequest GetDisplayRequestNoLock()
    {
        DisplayRequest? displayRequest = _displayRequest;
        if (displayRequest is null)
        {
            displayRequest = new();
            _displayRequest = displayRequest;
        }

        return displayRequest;
    }
}
