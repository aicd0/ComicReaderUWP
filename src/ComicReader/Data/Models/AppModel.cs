// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.Constants;
using ComicReader.SDK.Common.KVStorage;

namespace ComicReader.Data.Models;

static class AppModel
{
    public static int DefaultArchiveCodePage
    {
        get
        {
            return (int)KVDatabase.Default.GetLong(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_DEFAULT_ARCHIVE_CODE_PAGE, -1);
        }
        set
        {
            KVDatabase.Default.SetLong(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_DEFAULT_ARCHIVE_CODE_PAGE, value);
        }
    }

    public static bool AntiAliasingEnabled
    {
        get
        {
            return KVDatabase.Default.GetBoolean(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_ANTI_ALIASING_ENABLED, false);
        }
        set
        {
            KVDatabase.Default.SetBoolean(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_ANTI_ALIASING_ENABLED, value);
        }
    }

    public static bool SaveBrowsingHistory
    {
        get
        {
            return KVDatabase.Default.GetBoolean(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_SAVE_BROWSING_HISTORY, true);
        }
        set
        {
            KVDatabase.Default.SetBoolean(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_SAVE_BROWSING_HISTORY, value);
        }
    }

    public static bool TransitionAnimation
    {
        get
        {
            return KVDatabase.Default.GetBoolean(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_TRANSITION_ANIMATION, true);
        }
        set
        {
            KVDatabase.Default.SetBoolean(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_TRANSITION_ANIMATION, value);
        }
    }

    public static bool AutomaticallyHideCursor
    {
        get
        {
            return KVDatabase.Default.GetBoolean(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_AUTO_HIDE_CURSOR, false);
        }
        set
        {
            KVDatabase.Default.SetBoolean(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_AUTO_HIDE_CURSOR, value);
        }
    }
}
