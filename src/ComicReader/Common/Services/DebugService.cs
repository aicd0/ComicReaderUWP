// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.Utils;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.ServiceManagement;

namespace ComicReader.Common.Services;

internal class DebugService : IDebugService
{
    public void OnCrashReport(string info)
    {
        DialogUtils.DialogOptions options = new DialogUtils.DialogOptions.Builder()
            .SetTitle(StringResourceProvider.Instance.UnhandledExceptionTitle)
            .SetContent(StringResourceProvider.Instance.UnhandledExceptionContent.Replace("$info", info), selectable: true)
            .SetPrimaryButtonText(StringResourceProvider.Instance.OK)
            .SetSecondaryButtonText(StringResourceProvider.Instance.Copy)
            .OnSecondaryButtonClick((e) =>
            {
                ClipboardUtils.SetText(info);
                e.Cancel = true;
            })
            .Build();
        _ = DialogUtils.EnqueueDialogAsync(options);
    }

    public bool HandleDebugCommand(string command)
    {
        if (command == "dev_tools")
        {
            MainWindow.Open(url: RouterConstants.SCHEME_APP + RouterConstants.HOST_DEV_TOOLS);
            return true;
        }

        return false;
    }
}
