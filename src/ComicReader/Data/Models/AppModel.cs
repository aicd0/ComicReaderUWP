// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.Constants;
using ComicReader.SDK.Common.KVStorage;

namespace ComicReader.Data.Models;

static class AppModel
{
    private const string KEY_DEFAULT_ARCHIVE_CODE_PAGE = "default_archive_code_page";
    private const string KEY_ANTI_ALIASING_ENABLED = "anti_aliasing_enabled";
    private const string KEY_SAVE_BROWSING_HISTORY = "save_browsing_history";
    private const string KEY_TRANSITION_ANIMATION = "transition_animation";

    public static int DefaultArchiveCodePage
    {
        get
        {
            return (int)KVDatabase.Default.GetLong(DatabaseEntry.KV_LIB_APP, KEY_DEFAULT_ARCHIVE_CODE_PAGE, -1);
        }
        set
        {
            KVDatabase.Default.SetLong(DatabaseEntry.KV_LIB_APP, KEY_DEFAULT_ARCHIVE_CODE_PAGE, value);
        }
    }

    public static bool AntiAliasingEnabled
    {
        get
        {
            return KVDatabase.Default.GetBoolean(DatabaseEntry.KV_LIB_APP, KEY_ANTI_ALIASING_ENABLED, false);
        }
        set
        {
            KVDatabase.Default.SetBoolean(DatabaseEntry.KV_LIB_APP, KEY_ANTI_ALIASING_ENABLED, value);
        }
    }

    public static bool SaveBrowsingHistory
    {
        get
        {
            return KVDatabase.Default.GetBoolean(DatabaseEntry.KV_LIB_APP, KEY_SAVE_BROWSING_HISTORY, true);
        }
        set
        {
            KVDatabase.Default.SetBoolean(DatabaseEntry.KV_LIB_APP, KEY_SAVE_BROWSING_HISTORY, value);
        }
    }

    public static bool TransitionAnimation
    {
        get
        {
            return KVDatabase.Default.GetBoolean(DatabaseEntry.KV_LIB_APP, KEY_TRANSITION_ANIMATION, true);
        }
        set
        {
            KVDatabase.Default.SetBoolean(DatabaseEntry.KV_LIB_APP, KEY_TRANSITION_ANIMATION, value);
        }
    }
}
