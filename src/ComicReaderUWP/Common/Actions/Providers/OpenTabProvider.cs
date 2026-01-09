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

        string tabIdString = parameters[PARAM_TAB_ID] ?? string.Empty;
        int tabId;
        if (string.IsNullOrEmpty(tabIdString))
        {
            tabId = -2;
        }
        else if (!int.TryParse(tabIdString, out tabId) || tabId < -1)
        {
            context.SetError($"'{tabIdString}' is not a valid tab ID.");
            return;
        }

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

            if (tabId == -2)
            {
                tabId = window.CurrentTab?.Id ?? -1;
            }

            IMainPageComponent? mainPageCom = context.GetComponent<IMainPageComponent>();
            int initiateTabId = mainPageCom?.TabId ?? -1;
            window.OpenTab(url, tabId, initiateTabId);
        }

        context.SetSuccess();
    }
}
