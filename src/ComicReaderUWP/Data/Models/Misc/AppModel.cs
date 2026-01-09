// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.SDK.Common.Utils;
using ComicReaderUWP.SDK.Database.KV;

namespace ComicReaderUWP.Data.Models.Misc;

static class AppModel
{
    public static bool AntiAliasingEnabled
    {
        get
        {
            return KVStore.App.GetCollection(DatabaseEntry.KV_LIB_APP).GetValueOrDefault(DatabaseEntry.KV_KEY_APP_ANTI_ALIASING_ENABLED, false);
        }
        set
        {
            KVStore.App.GetCollection(DatabaseEntry.KV_LIB_APP).Set(DatabaseEntry.KV_KEY_APP_ANTI_ALIASING_ENABLED, value);
        }
    }

    public static bool AutomaticallyHideCursor
    {
        get
        {
            return KVStore.App.GetCollection(DatabaseEntry.KV_LIB_APP).GetValueOrDefault(DatabaseEntry.KV_KEY_APP_AUTO_HIDE_CURSOR, false);
        }
        set
        {
            KVStore.App.GetCollection(DatabaseEntry.KV_LIB_APP).Set(DatabaseEntry.KV_KEY_APP_AUTO_HIDE_CURSOR, value);
        }
    }

    public static int DefaultArchiveCodePage
    {
        get
        {
            return (int)KVStore.App.GetCollection(DatabaseEntry.KV_LIB_APP).GetValueOrDefault<long>(DatabaseEntry.KV_KEY_APP_DEFAULT_ARCHIVE_CODE_PAGE, -1);
        }
        set
        {
            KVStore.App.GetCollection(DatabaseEntry.KV_LIB_APP).Set<long>(DatabaseEntry.KV_KEY_APP_DEFAULT_ARCHIVE_CODE_PAGE, value);
        }
    }

    public static bool RatingPercentageEnabled
    {
        get
        {
            return KVStore.App.GetCollection(DatabaseEntry.KV_LIB_APP).GetValueOrDefault(DatabaseEntry.KV_KEY_APP_RATING_PERCENTAGE_ENABLED, false);
        }
        set
        {
            KVStore.App.GetCollection(DatabaseEntry.KV_LIB_APP).Set(DatabaseEntry.KV_KEY_APP_RATING_PERCENTAGE_ENABLED, value);
        }
    }

    public static bool SaveBrowsingHistory
    {
        get
        {
            return KVStore.App.GetCollection(DatabaseEntry.KV_LIB_APP).GetValueOrDefault(DatabaseEntry.KV_KEY_APP_SAVE_BROWSING_HISTORY, true);
        }
        set
        {
            KVStore.App.GetCollection(DatabaseEntry.KV_LIB_APP).Set(DatabaseEntry.KV_KEY_APP_SAVE_BROWSING_HISTORY, value);
        }
    }

    public static bool TransitionAnimation
    {
        get
        {
            return KVStore.App.GetCollection(DatabaseEntry.KV_LIB_APP).GetValueOrDefault(DatabaseEntry.KV_KEY_APP_TRANSITION_ANIMATION, true);
        }
        set
        {
            KVStore.App.GetCollection(DatabaseEntry.KV_LIB_APP).Set(DatabaseEntry.KV_KEY_APP_TRANSITION_ANIMATION, value);
        }
    }
}
