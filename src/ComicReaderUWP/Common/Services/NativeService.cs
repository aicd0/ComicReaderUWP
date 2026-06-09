// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Core.Common.ServiceManagement.Models;
using ComicReaderUWP.Core.Common.ServiceManagement.Services;

namespace ComicReaderUWP.Common.Services;

internal class NativeService : INativeService
{
    public NativeDialogResult ShowYesNoDialog(string caption, string text)
    {
        NativeDialog.DialogResult result = NativeDialog.ShowYesNo(caption, text);
        return result switch
        {
            NativeDialog.DialogResult.Yes => NativeDialogResult.Yes,
            NativeDialog.DialogResult.No => NativeDialogResult.No,
            _ => NativeDialogResult.Cancel,
        };
    }
}
