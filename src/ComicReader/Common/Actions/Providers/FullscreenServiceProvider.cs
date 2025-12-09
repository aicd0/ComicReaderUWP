// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Specialized;

using ComicReader.Common.Actions.Components;
using ComicReader.Views.AppWindows.Main;

namespace ComicReader.Common.Actions.Providers;

internal class FullscreenServiceProvider : IActionProvider
{
    public const string NAME = "FullscreenService";
    public const string PARAM_WINDOW_ID = "WindowID";
    public const string PARAM_ENTER = "Enter";

    public string Name => NAME;

    public void Handle(IActionProviderContext context, NameValueCollection parameters)
    {
        string windowIdString = parameters[PARAM_WINDOW_ID] ?? string.Empty;
        int windowId;
        if (string.IsNullOrEmpty(windowIdString))
        {
            IMainWindowComponent? mainWindowCom = context.GetComponent<IMainWindowComponent>();
            if (mainWindowCom is null)
            {
                context.SetError("IMainWindowComponent component not found.");
                return;
            }

            windowId = mainWindowCom.WindowId;
        }
        else if (!int.TryParse(windowIdString, out windowId) || windowId < 0)
        {
            context.SetError($"'{windowIdString}' is not a valid window ID.");
            return;
        }

        string enterString = parameters[PARAM_ENTER] ?? string.Empty;
        if (enterString != "0" && enterString != "1")
        {
            context.SetError($"Invalid Enter parameter '{enterString}'.");
            return;
        }

        bool enter = enterString == "1";
        MainWindow? window = App.Instance.WindowManager.GetWindow(windowId);
        if (window is null)
        {
            context.SetError($"Window {windowId} not found.");
            return;
        }

        if (enter)
        {
            window.EnterFullscreen();
        }
        else
        {
            window.ExitFullscreen();
        }

        context.SetSuccess();
    }
}
