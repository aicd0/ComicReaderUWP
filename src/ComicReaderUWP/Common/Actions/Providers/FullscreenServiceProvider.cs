// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Specialized;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Actions.Components;
using ComicReaderUWP.Views.AppWindows.Main;

namespace ComicReaderUWP.Common.Actions.Providers;

internal class FullscreenServiceProvider : IActionProvider
{
    public const string NAME = "FullscreenService";
    public const string PARAM_WINDOW_ID = "WindowID";
    public const string PARAM_ENTER = "Enter";

    public string Name => NAME;

    public async Task<ActionResult> Handle(IActionProviderContext context, NameValueCollection parameters)
    {
        string windowIdString = parameters[PARAM_WINDOW_ID] ?? string.Empty;
        int windowId;
        if (string.IsNullOrEmpty(windowIdString))
        {
            IMainWindowComponent? mainWindowCom = context.GetComponent<IMainWindowComponent>();
            if (mainWindowCom is null)
            {
                return ActionResult.FromFailure("IMainWindowComponent component not found.");
            }

            windowId = mainWindowCom.WindowId;
        }
        else if (!int.TryParse(windowIdString, out windowId) || windowId < 0)
        {
            return ActionResult.FromFailure($"'{windowIdString}' is not a valid window ID.");
        }

        string enterString = parameters[PARAM_ENTER] ?? string.Empty;
        if (enterString != "0" && enterString != "1")
        {
            return ActionResult.FromFailure($"Invalid Enter parameter '{enterString}'.");
        }

        bool enter = enterString == "1";
        MainWindow? window = App.Instance.WindowManager.GetWindow(windowId);
        if (window is null)
        {
            return ActionResult.FromFailure($"Window {windowId} not found.");
        }

        if (enter)
        {
            window.EnterFullscreen();
        }
        else
        {
            window.ExitFullscreen();
        }

        return ActionResult.FromSuccess();
    }
}
