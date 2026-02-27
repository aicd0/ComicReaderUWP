// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Helpers.Navigation;

internal static class RouterConstants
{
    public const string SCHEME_APP_NO_PREFIX = "comicreader";
    public const string SCHEME_APP = SCHEME_APP_NO_PREFIX + "://";

    public const string HOST_MAIN = "main";
    public const string HOST_READER = "reader";
    public const string HOST_HOME = "home";
    public const string HOST_SEARCH = "search";
    public const string HOST_SETTINGS = "settings";
    public const string HOST_SIDE_PANE_FAVORITE = "side_pane_favorite";
    public const string HOST_SIDE_PANE_HISTORY = "side_pane_history";
    public const string HOST_SIDE_PANE_FOLDERS = "side_pane_folders";
    public const string HOST_SIDE_PANE_TAGS = "side_pane_tags";
    public const string HOST_SIDE_PANE_FILTER_PRESETS = "side_pane_filter_presets";
    public const string HOST_SIDE_PANE_PLAYLIST = "side_pane_playlist";
    public const string HOST_SIDE_PANE_COMIC_INFO = "side_pane_comic_info";
    public const string HOST_DEV_TOOLS = "dev_tools";

    public const string ARG_FILTER_JSON = "filter_json";
    public const string ARG_KEYWORD = "keyword";
    public const string ARG_PLAYBACK = "playback";
    public const string ARG_PLAYLIST_ID = "playlist_id";
}
