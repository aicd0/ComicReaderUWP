// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace ComicReaderUWP.Common.HotKey;

public static class KeyboardKeyNames
{
    private const int MaximumNameLength = 64;

    /// <summary>
    /// Returns the name the active keyboard layout gives the key, which is the only source for keys such as the
    /// punctuation keys that the virtual key enumeration leaves unnamed.
    /// </summary>
    public static string? GetName(uint virtualKey)
    {
        uint scanCode = PInvoke.MapVirtualKey(virtualKey, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC);
        if (scanCode == 0)
        {
            return null;
        }

        Span<char> buffer = stackalloc char[MaximumNameLength];
        int length = PInvoke.GetKeyNameText((int)(scanCode << 16), buffer);
        return length > 0 ? buffer[..length].ToString() : null;
    }
}
