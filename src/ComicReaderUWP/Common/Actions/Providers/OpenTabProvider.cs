// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Specialized;

using ComicReaderUWP.Common.Actions.Components;
using ComicReaderUWP.Views.AppWindows.Main;

namespace ComicReaderUWP.Common.Actions.Providers;

internal class OpenTabProvider : IActionProvider
{
    public const string NAME = "OpenTab";
    public const string PARAM_URL = "URL";
    public const string PARAM_WINDOW_ID = "WindowId";
    public const string PARAM_TAB_ID = "TabId";

    public string Name => NAME;

    public void Handle(IActionProviderContext context, NameValueCollection parameters)
    {
        string url = parameters[PARAM_URL] ?? string.Empty;
        if (string.IsNullOrEmpty(url))
        {
            context.SetError("URL missing.");
            return;
        }

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
        else if (!int.TryParse(windowIdString, out windowId) || windowId < -1)
        {
            context.SetError($"'{windowIdString}' is not a valid window ID.");
            return;
        }

        string? tabId = parameters[PARAM_TAB_ID];

        if (windowId == -1)
        {
            MainWindow.Open(url);
        }
        else
        {
            MainWindow? window = App.Instance.WindowManager.GetWindow(windowId);
            if (window is null)
            {
                context.SetError($"Window {windowId} not found.");
                return;
            }

            tabId ??= window.CurrentTab?.Id ?? string.Empty;
            IMainPageComponent? mainPageCom = context.GetComponent<IMainPageComponent>();
            string initiateTabId = mainPageCom?.TabId ?? string.Empty;
            window.OpenTab(url, tabId, initiateTabId);
        }

        context.SetSuccess();
    }
}
