// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Threading;

using ComicReader.Common.Lifecycle;
using ComicReader.SDK.Common.DebugTools;

using Windows.Win32;

namespace ComicReader.Common;

internal class HotKeyManager
{
    private const string TAG = nameof(HotKeyManager);

    // https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
    private const uint VK_F10 = 0x79;
    private const uint VK_F11 = 0x7A;

    private const int HOTKEY_ID_F10 = 1;
    private const int HOTKEY_ID_F11 = 2;

    public static HotKeyManager Instance { get; } = new();

    private int _isRegistered = 0;
    private readonly ConcurrentDictionary<int, IMutableLiveData<object>> _hotKeyEvents = [];

    private HotKeyManager() { }

    public void RegisterHotKeys(nint windowHandle)
    {
        if (Interlocked.CompareExchange(ref _isRegistered, 1, 0) == 1)
        {
            return;
        }

        Windows.Win32.Foundation.HWND hwnd = new(windowHandle.ToInt32());
        RegisterHotKey(hwnd, HOTKEY_ID_F10, 0, VK_F10, GlobalEvent.Instance.HotKeyF10);
        RegisterHotKey(hwnd, HOTKEY_ID_F11, 0, VK_F11, GlobalEvent.Instance.HotKeyF11);
    }

    public void UnregisterHotKeys(nint windowHandle)
    {
        if (Interlocked.CompareExchange(ref _isRegistered, 0, 1) == 0)
        {
            return;
        }

        Windows.Win32.Foundation.HWND hwnd = new(windowHandle.ToInt32());
        UnregisterHotKey(hwnd, HOTKEY_ID_F10);
        UnregisterHotKey(hwnd, HOTKEY_ID_F11);
    }

    public bool HandleHotKey(int hotkeyId)
    {
        if (!_hotKeyEvents.TryGetValue(hotkeyId, out IMutableLiveData<object>? liveData))
        {
            Logger.W(TAG, $"Unknown hotkey ID: {hotkeyId}");
            return false;
        }

        liveData.Emit(0);
        return true;
    }

    private void RegisterHotKey(Windows.Win32.Foundation.HWND hwnd, int id, Windows.Win32.UI.Input.KeyboardAndMouse.HOT_KEY_MODIFIERS modifiers, uint vk, IMutableLiveData<object> liveData)
    {
        Windows.Win32.Foundation.BOOL success = PInvoke.RegisterHotKey(hwnd, id, modifiers, vk);
        if (success)
        {
            _hotKeyEvents[id] = liveData;
        }
        else
        {
            Logger.E(TAG, $"Failed to register hotkey ID {id}.");
        }
    }

    private void UnregisterHotKey(Windows.Win32.Foundation.HWND hwnd, int id)
    {
        Windows.Win32.Foundation.BOOL success = PInvoke.UnregisterHotKey(hwnd, id);
        if (success)
        {
            _hotKeyEvents.TryRemove(id, out _);
        }
        else
        {
            Logger.W(TAG, $"Failed to unregister hotkey ID {id}.");
        }
    }
}
