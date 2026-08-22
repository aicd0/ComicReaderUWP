// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Specialized;
using System.Threading.Tasks;

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

    public async Task<ActionResult> Handle(IActionProviderContext context, NameValueCollection parameters)
    {
        string url = parameters[PARAM_URL] ?? string.Empty;
        if (string.IsNullOrEmpty(url))
        {
            return ActionResult.FromFailure("URL missing.");
        }

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
        else if (!int.TryParse(windowIdString, out windowId) || windowId < -1)
        {
            return ActionResult.FromFailure($"'{windowIdString}' is not a valid window ID.");
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
                return ActionResult.FromFailure($"Window {windowId} not found.");
            }

            tabId ??= window.CurrentTab?.Id ?? string.Empty;
            IMainPageComponent? mainPageCom = context.GetComponent<IMainPageComponent>();
            string initiateTabId = mainPageCom?.TabId ?? string.Empty;
            window.OpenTab(url, tabId, initiateTabId);
        }

        return ActionResult.FromSuccess();
    }
}
