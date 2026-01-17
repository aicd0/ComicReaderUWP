// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Actions.Providers;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.Views.Pages.Reader;

namespace ComicReaderUWP.Helpers.Misc;

internal static class OpenComicHelper
{
    private const string TAG = nameof(OpenComicHelper);

    public static Route GetComicRoute(ComicModel comic, PlaylistModel.Builder? playlist)
    {
        playlist ??= PlaylistModel.Builder.Create();
        var playback = PlaybackModel.Builder.Create();
        playback.SetCurrentId(playlist.EnsureComic(comic));
        return Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
            .WithParam(RouterConstants.ARG_PLAYLIST, playlist.ToSerializedString())
            .WithParam(RouterConstants.ARG_PLAYBACK, playback.ToSerializedString());
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
                        .AddParameter(OpenTabProvider.PARAM_TAB_ID, string.Empty)
                        .Build();
                    actionHandler.Handle(actionModel);
                }
                break;
            case AppSettingsModel.OpenComicBehaviorEnum.OpenInLastActiveReaderTab:
                {
                    IReadOnlyList<Tuple<int, string>> activeReaderTabs = ReaderPage.ActiveTabs;
                    if (activeReaderTabs.Count == 0)
                    {
                        goto case AppSettingsModel.OpenComicBehaviorEnum.OpenInNewTab;
                    }

                    int windowId = activeReaderTabs[activeReaderTabs.Count - 1].Item1;
                    string tabId = activeReaderTabs[activeReaderTabs.Count - 1].Item2;
                    ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                        .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
                        .AddParameter(OpenTabProvider.PARAM_WINDOW_ID, windowId.ToString())
                        .AddParameter(OpenTabProvider.PARAM_TAB_ID, tabId)
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
