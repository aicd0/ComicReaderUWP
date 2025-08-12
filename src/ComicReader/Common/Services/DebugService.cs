// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.Utils;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.ServiceManagement;

using Microsoft.UI.Xaml;

namespace ComicReader.Common.Services;

internal class DebugService : IDebugService
{
    public void OnCrashReport(string info)
    {
        MainWindow? window = App.WindowManager.GetActiveWindow() ?? App.WindowManager.GetAnyWindow();
        if (window is not null)
        {
            XamlRoot? xamlRoot = (window.Content as FrameworkElement)?.XamlRoot;
            if (xamlRoot is not null)
            {
                DialogUtils.DialogOptions options = new DialogUtils.DialogOptions.Builder()
                    .SetTitle(StringResourceProvider.Instance.UnhandledExceptionTitle)
                    .SetContent(StringResourceProvider.Instance.UnhandledExceptionContent.Replace("$info", info))
                    .SetPrimaryButtonText(StringResourceProvider.Instance.OK)
                    .SetSecondaryButtonText(StringResourceProvider.Instance.Copy)
                    .OnSecondaryButtonClick((e) =>
                    {
                        ClipboardUtils.SetText(info);
                        e.Cancel = true;
                    })
                    .Build();
                _ = DialogUtils.ShowDialogAsync(xamlRoot, options);
            }
        }
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
