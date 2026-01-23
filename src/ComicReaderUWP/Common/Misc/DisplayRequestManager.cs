// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.SDK.Common.DebugTools;

using Windows.System.Display;

namespace ComicReaderUWP.Common.Misc;

internal static class DisplayRequestManager
{
    private const string TAG = nameof(DisplayRequestManager);

    private static DisplayRequest? _displayRequest;
    private static int _keepScreenOnCounter = 0;

    public static void IncrememtKeepScreenOn()
    {
        DisplayRequest displayRequest = GetDisplayRequest();
        lock (displayRequest)
        {
            _keepScreenOnCounter++;
            if (_keepScreenOnCounter == 1)
            {
                displayRequest.RequestActive();
                Logger.I(TAG, "RequestActive");
            }
        }
    }

    public static void DecrememtKeepScreenOn()
    {
        DisplayRequest displayRequest = GetDisplayRequest();
        lock (displayRequest)
        {
            if (_keepScreenOnCounter == 0)
            {
                return;
            }

            _keepScreenOnCounter--;
            if (_keepScreenOnCounter == 0)
            {
                displayRequest.RequestRelease();
                Logger.I(TAG, "RequestRelease");
            }
        }
    }

    private static DisplayRequest GetDisplayRequest()
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
