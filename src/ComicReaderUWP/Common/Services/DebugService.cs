// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.ServiceManagement.Services;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.SDK.Models;

namespace ComicReaderUWP.Common.Services;

internal class DebugService : IDebugService
{
    public bool SentryEnabled => AppSettingsModel.SendUsageData;

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
}
