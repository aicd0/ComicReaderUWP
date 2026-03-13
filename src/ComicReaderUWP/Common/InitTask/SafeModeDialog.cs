// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.Localization;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ComicReaderUWP.Common.InitTask;

internal static class SafeModeDialog
{
    public static DialogResult Show()
    {
        MESSAGEBOX_RESULT result = PInvoke.MessageBox(
            HWND.Null,
            StringResourceProvider.Instance.SafeModeMessage,
            StringResourceProvider.Instance.AppDisplayName,
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
