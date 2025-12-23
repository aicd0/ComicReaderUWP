// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.Constants;
using ComicReader.SDK.Common.Utils;
using ComicReader.SDK.Database.KV;

namespace ComicReader.Data.Models;

static class AppModel
{
    public static bool AntiAliasingEnabled
    {
        get
        {
            return KVStore.Default.GetCollection(DatabaseEntry.KV_LIB_APP).GetValueOrDefault(DatabaseEntry.KV_KEY_APP_ANTI_ALIASING_ENABLED, false);
        }
        set
        {
            KVStore.Default.GetCollection(DatabaseEntry.KV_LIB_APP).Set(DatabaseEntry.KV_KEY_APP_ANTI_ALIASING_ENABLED, value);
        }
    }

    public static bool AutomaticallyHideCursor
    {
        get
        {
            return KVStore.Default.GetCollection(DatabaseEntry.KV_LIB_APP).GetValueOrDefault(DatabaseEntry.KV_KEY_APP_AUTO_HIDE_CURSOR, false);
        }
        set
        {
            KVStore.Default.GetCollection(DatabaseEntry.KV_LIB_APP).Set(DatabaseEntry.KV_KEY_APP_AUTO_HIDE_CURSOR, value);
        }
    }

    public static int DefaultArchiveCodePage
    {
        get
        {
            return (int)KVStore.Default.GetCollection(DatabaseEntry.KV_LIB_APP).GetValueOrDefault<long>(DatabaseEntry.KV_KEY_APP_DEFAULT_ARCHIVE_CODE_PAGE, -1);
        }
        set
        {
            KVStore.Default.GetCollection(DatabaseEntry.KV_LIB_APP).Set<long>(DatabaseEntry.KV_KEY_APP_DEFAULT_ARCHIVE_CODE_PAGE, value);
        }
    }

    public static bool RatingPercentageEnabled
    {
        get
        {
            return KVStore.Default.GetCollection(DatabaseEntry.KV_LIB_APP).GetValueOrDefault(DatabaseEntry.KV_KEY_APP_RATING_PERCENTAGE_ENABLED, false);
        }
        set
        {
            KVStore.Default.GetCollection(DatabaseEntry.KV_LIB_APP).Set(DatabaseEntry.KV_KEY_APP_RATING_PERCENTAGE_ENABLED, value);
        }
    }

    public static bool SaveBrowsingHistory
    {
        get
        {
            return KVStore.Default.GetCollection(DatabaseEntry.KV_LIB_APP).GetValueOrDefault(DatabaseEntry.KV_KEY_APP_SAVE_BROWSING_HISTORY, true);
        }
        set
        {
            KVStore.Default.GetCollection(DatabaseEntry.KV_LIB_APP).Set(DatabaseEntry.KV_KEY_APP_SAVE_BROWSING_HISTORY, value);
        }
    }

    public static bool TransitionAnimation
    {
        get
        {
            return KVStore.Default.GetCollection(DatabaseEntry.KV_LIB_APP).GetValueOrDefault(DatabaseEntry.KV_KEY_APP_TRANSITION_ANIMATION, true);
        }
        set
        {
            KVStore.Default.GetCollection(DatabaseEntry.KV_LIB_APP).Set(DatabaseEntry.KV_KEY_APP_TRANSITION_ANIMATION, value);
        }
    }
}
