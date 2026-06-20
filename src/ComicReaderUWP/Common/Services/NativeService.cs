// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.ServiceManagement.Models;
using ComicReaderUWP.Core.Common.ServiceManagement.Services;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ComicReaderUWP.Common.Services;

internal class NativeService : INativeService
{
    public NativeDialogResult ShowDialog(NativeDialogButtonType buttonType, NativeDialogIconType iconType, string caption, string text)
    {
        MESSAGEBOX_STYLE buttonStyle = buttonType switch
        {
            NativeDialogButtonType.OK => MESSAGEBOX_STYLE.MB_OK,
            NativeDialogButtonType.OKCancel => MESSAGEBOX_STYLE.MB_OKCANCEL,
            NativeDialogButtonType.YesNo => MESSAGEBOX_STYLE.MB_YESNO,
            NativeDialogButtonType.YesNoCancel => MESSAGEBOX_STYLE.MB_YESNOCANCEL,
            _ => MESSAGEBOX_STYLE.MB_OK,
        };
        MESSAGEBOX_STYLE iconStyle = iconType switch
        {
            NativeDialogIconType.Info => MESSAGEBOX_STYLE.MB_ICONINFORMATION,
            NativeDialogIconType.Warning => MESSAGEBOX_STYLE.MB_ICONWARNING,
            NativeDialogIconType.Error => MESSAGEBOX_STYLE.MB_ICONERROR,
            _ => MESSAGEBOX_STYLE.MB_ICONINFORMATION,
        };
        MESSAGEBOX_RESULT result = PInvoke.MessageBox(
            HWND.Null,
            text,
            caption,
            buttonStyle | iconStyle);
        return result switch
        {
            MESSAGEBOX_RESULT.IDOK => NativeDialogResult.OK,
            MESSAGEBOX_RESULT.IDYES => NativeDialogResult.Yes,
            MESSAGEBOX_RESULT.IDNO => NativeDialogResult.No,
            MESSAGEBOX_RESULT.IDCANCEL => NativeDialogResult.Cancel,
            _ => NativeDialogResult.Cancel,
        };
    }
}
