// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ComicReaderUWP.Common.InitTask;

internal static class SafeModeDialog
{
    public static bool Show()
    {
        MESSAGEBOX_RESULT result = PInvoke.MessageBox(
            HWND.Null,
            "The application didn't exit normally last time.\nStart in Safe Mode?",
            "Comic Reader UWP",
            MESSAGEBOX_STYLE.MB_YESNO | MESSAGEBOX_STYLE.MB_ICONWARNING);
        return result == MESSAGEBOX_RESULT.IDYES;
    }
}
