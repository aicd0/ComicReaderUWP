// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReader.Common.Actions;
using ComicReader.Common.Actions.Providers;
using ComicReader.Data.Models.Misc;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.Views.Pages.Reader;

namespace ComicReader.Helpers.Misc;

internal static class OpenComicHelper
{
    private const string TAG = nameof(OpenComicHelper);

    public static void OpenComic(ActionHandler actionHandler, long comicId)
    {
        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
            .WithParam(RouterConstants.ARG_COMIC_ID, comicId.ToString());
        OpenComic(actionHandler, route);
    }

    public static void OpenComic(ActionHandler actionHandler, Route route)
    {
        AppSettingsModel.OpenComicBehaviorEnum behavior = AppSettingsModel.Instance.OpenComicDefaultBehavior;
        switch (behavior)
        {
            case AppSettingsModel.OpenComicBehaviorEnum.OpenInCurrentTab:
                {
                    ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                        .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
                        .Build();
                    actionHandler.Handle(actionModel);
                }
                break;
            case AppSettingsModel.OpenComicBehaviorEnum.OpenInNewTab:
                {
                    ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                        .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
                        .AddParameter(OpenTabProvider.PARAM_TAB_ID, "-1")
                        .Build();
                    actionHandler.Handle(actionModel);
                }
                break;
            case AppSettingsModel.OpenComicBehaviorEnum.OpenInLastActiveReaderTab:
                {
                    IReadOnlyList<Tuple<int, int>> activeReaderTabs = ReaderPage.ActiveTabs;
                    if (activeReaderTabs.Count == 0)
                    {
                        goto case AppSettingsModel.OpenComicBehaviorEnum.OpenInNewTab;
                    }

                    int windowId = activeReaderTabs[activeReaderTabs.Count - 1].Item1;
                    int tabId = activeReaderTabs[activeReaderTabs.Count - 1].Item2;
                    ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                        .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
                        .AddParameter(OpenTabProvider.PARAM_WINDOW_ID, windowId.ToString())
                        .AddParameter(OpenTabProvider.PARAM_TAB_ID, tabId.ToString())
                        .Build();
                    actionHandler.Handle(actionModel);
                }
                break;
            default:
                Logger.F(TAG, $"Unknown enum value {behavior}");
                goto case AppSettingsModel.OpenComicBehaviorEnum.OpenInCurrentTab;
        }
    }
}
