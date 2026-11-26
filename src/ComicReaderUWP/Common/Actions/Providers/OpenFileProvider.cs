// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Specialized;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Actions.Components;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Views.AppWindows.Main;

using Windows.Storage;

namespace ComicReaderUWP.Common.Actions.Providers;

internal class OpenFileProvider : IActionProvider
{
    public const string NAME = "OpenFile";

    public string Name => NAME;

    public async Task<ActionResult> Handle(IActionProviderContext context, NameValueCollection parameters)
    {
        IMainWindowComponent? mainWindowCom = context.GetComponent<IMainWindowComponent>();
        if (mainWindowCom is null)
        {
            return ActionResult.FromFailure("IMainWindowComponent component not found.");
        }

        StorageFile? file = await FilePickerUtils.PickFile(mainWindowCom.WindowId, [.. AppInfoProvider.SupportedExternalFileExtensions]);
        if (file is null)
        {
            return ActionResult.FromSuccess();
        }

        MainWindow? window = App.Instance.WindowManager.GetWindow(mainWindowCom.WindowId);
        if (window is null)
        {
            return ActionResult.FromFailure($"Window {mainWindowCom.WindowId} not found.");
        }

        await App.Instance.OnCommandLine(window, [file.Path]);
        return ActionResult.FromSuccess();
    }
}
