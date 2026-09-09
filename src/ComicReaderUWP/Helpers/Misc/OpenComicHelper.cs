// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Actions.Providers;
using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Data.Models.Playback;
using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.Views.Pages.Reader;

namespace ComicReaderUWP.Helpers.Misc;

internal static class OpenComicHelper
{
    private const string TAG = nameof(OpenComicHelper);

    public static Route GetComicRoute(
        ComicModel comic,
        double page = -1.0,
        PlaylistModel.Builder? playlist = null,
        PlaybackModel.Builder? playback = null)
    {
        playlist ??= new PlaylistModel.Builder();
        string itemId = playlist.EnsureComic(comic);

        playback ??= new PlaybackModel.Builder();
        playback.CurrentId = itemId;

        string playlistId = Guid.NewGuid().ToString();
        AppDB.MainRegistry.CreateKey(RegistryNames.PLAYLISTS).Set(playlistId, playlist.ToSerializedString());

        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
            .WithParam(RouterConstants.ARG_PLAYLIST_ID, playlistId)
            .WithParam(RouterConstants.ARG_PLAYBACK, playback.ToSerializedString());

        if (double.IsFinite(page) && page >= 0.0)
        {
            route = route.WithParam(RouterConstants.ARG_PAGE, page.ToString(CultureInfo.InvariantCulture));
        }

        return route;
    }

    public static void OpenComic(ActionHandler actionHandler, Route route)
    {
        AppSettingsModel.OpenComicBehaviorEnum behavior = AppSettingsModel.OpenComicDefaultBehavior;
        switch (behavior)
        {
            case AppSettingsModel.OpenComicBehaviorEnum.OpenInCurrentTab:
                {
                    ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                        .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
                        .Build();
                    actionHandler.HandleNoResult(actionModel);
                }
                break;
            case AppSettingsModel.OpenComicBehaviorEnum.OpenInNewTab:
                {
                    ActionModel actionModel = ActionModel.Builder.Create(OpenTabProvider.NAME)
                        .AddParameter(OpenTabProvider.PARAM_URL, route.Url)
                        .AddParameter(OpenTabProvider.PARAM_TAB_ID, string.Empty)
                        .Build();
                    actionHandler.HandleNoResult(actionModel);
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
                    actionHandler.HandleNoResult(actionModel);
                }
                break;
            default:
                Logger.F(TAG, $"Unknown enum value {behavior}");
                goto case AppSettingsModel.OpenComicBehaviorEnum.OpenInCurrentTab;
        }
    }
}
