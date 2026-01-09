// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.SDK.Common.DebugTools;

using Microsoft.UI.Xaml.Input;

namespace ComicReaderUWP.Common.Misc;

internal static class FocusTracker
{
    private const string TAG = nameof(FocusTracker);

    private readonly static LogTag _gotFocusTag = LogTag.N(TAG, "GotFocus");
    private readonly static LogTag _losingFocusTag = LogTag.N(TAG, "LosingFocus");
    private readonly static LogTag _lostFocusTag = LogTag.N(TAG, "LostFocus");

    public static void Initialize()
    {
        FocusManager.GotFocus += FocusManager_GotFocus;
        FocusManager.LosingFocus += FocusManager_LosingFocus;
        FocusManager.LostFocus += FocusManager_LostFocus;
    }

    private static void FocusManager_GotFocus(object? sender, FocusManagerGotFocusEventArgs e)
    {
        Logger.D(_gotFocusTag, $"Ele={e.NewFocusedElement}");
    }

    private static void FocusManager_LosingFocus(object? sender, LosingFocusEventArgs e)
    {
        Logger.D(_losingFocusTag, $"St={e.FocusState},D={e.Direction},OldEle={e.OldFocusedElement},NewEle={e.NewFocusedElement}");
    }

    private static void FocusManager_LostFocus(object? sender, FocusManagerLostFocusEventArgs e)
    {
        Logger.D(_lostFocusTag, $"Ele={e.OldFocusedElement}");
    }
}
