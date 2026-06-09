// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.ServiceManagement.Services;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.SDK.Models;
using ComicReaderUWP.Views.AppWindows.Main;

namespace ComicReaderUWP.Common.Services;

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
        CoroutineUtils.Run(() => DialogUtils.EnqueueDialogAsync(options));
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
