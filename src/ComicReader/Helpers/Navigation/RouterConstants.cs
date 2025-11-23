// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Helpers.Navigation;

internal static class RouterConstants
{
    public const string SCHEME_APP_NO_PREFIX = "comicreader";
    public const string SCHEME_APP = SCHEME_APP_NO_PREFIX + "://";

    public const string HOST_MAIN = "main";
    public const string HOST_READER = "reader";
    public const string HOST_HOME = "home";
    public const string HOST_SEARCH = "search";
    public const string HOST_SETTING = "setting";
    public const string HOST_SIDE_PANE_FAVORITE = "side_pane_favorite";
    public const string HOST_SIDE_PANE_HISTORY = "side_pane_history";
    public const string HOST_SIDE_PANE_TAGS = "side_pane_tags";
    public const string HOST_NAVIGATION = "navigation";
    public const string HOST_DEV_TOOLS = "dev_tools";

    public const string ARG_WINDOW_ID = "window_id";
    public const string ARG_COMIC_ID = "comic_id";
    public const string ARG_COMIC_LOCATION = "comic_location";
    public const string ARG_KEYWORD = "keyword";
}
