// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ComicReaderUWP.Common.Misc;

internal static class NativeDialog
{
    public static DialogResult ShowYesNo(string caption, string text)
    {
        MESSAGEBOX_RESULT result = PInvoke.MessageBox(
            HWND.Null,
            text,
            caption,
            MESSAGEBOX_STYLE.MB_YESNO | MESSAGEBOX_STYLE.MB_ICONINFORMATION);
        return result switch
        {
            MESSAGEBOX_RESULT.IDYES => DialogResult.Yes,
            MESSAGEBOX_RESULT.IDNO => DialogResult.No,
            _ => DialogResult.Cancel,
        };
    }

    public static DialogResult ShowYesNoCancel(string caption, string text)
    {
        MESSAGEBOX_RESULT result = PInvoke.MessageBox(
            HWND.Null,
            text,
            caption,
            MESSAGEBOX_STYLE.MB_YESNOCANCEL | MESSAGEBOX_STYLE.MB_ICONINFORMATION);
        return result switch
        {
            MESSAGEBOX_RESULT.IDYES => DialogResult.Yes,
            MESSAGEBOX_RESULT.IDNO => DialogResult.No,
            MESSAGEBOX_RESULT.IDCANCEL => DialogResult.Cancel,
            _ => DialogResult.Cancel,
        };
    }

    public enum DialogResult
    {
        Yes,
        No,
        Cancel
    }
}
