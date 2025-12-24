// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.Utils;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.ServiceManagement;
using ComicReader.SDK.Common.Utils;
using ComicReader.SDK.DataModels;
using ComicReader.Views.AppWindows.Main;

namespace ComicReader.Common.Services;

internal class DebugService : IDebugService
{
    public void OnCrashReport(string info)
    {
        DialogOptions options = new DialogOptions.Builder()
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
        CoroutineUtils.Start(() => DialogUtils.EnqueueDialogAsync(options));
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
