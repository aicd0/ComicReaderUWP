// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.Constants;
using ComicReader.SDK.Common.DebugTools;

using Windows.Win32;

namespace ComicReader.Common;

internal class HotKeyManager(int windowId)
{
    private const string TAG = nameof(HotKeyManager);
    private const int HOTKEY_ID_F11 = 1;
    private const uint VK_F11 = 0x7A;

    private readonly int _windowId = windowId;

    public void RegisterHotKeys(nint windowHandle)
    {
        Windows.Win32.Foundation.HWND hwnd = new(windowHandle.ToInt32());
        Windows.Win32.Foundation.BOOL success = PInvoke.RegisterHotKey(hwnd, HOTKEY_ID_F11, 0, VK_F11);
        if (!success)
        {
            Logger.E(TAG, "Failed to register F11 hotkey.");
        }
    }

    public bool HandleHotKey(int hotkeyId)
    {
        bool handled = true;
        switch (hotkeyId)
        {
            case HOTKEY_ID_F11:
                DispatchHotKeyEvent(EventId.HotKeyF11);
                break;
            default:
                handled = false;
                break;
        }

        return handled;
    }

    private void DispatchHotKeyEvent(string eventName)
    {
        App.WindowManager.GetEventBus(_windowId).With(eventName).Emit(0);
    }
}
