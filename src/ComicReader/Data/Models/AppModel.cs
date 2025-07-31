// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Concurrent;
using System.Threading;

using ComicReader.Common.Constants;
using ComicReader.Data.Models.Comic;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.KVStorage;

namespace ComicReader.Data.Models;

static class AppModel
{
    private const string TAG = "AppStatusPreserver";
    private const string KEY_DEFAULT_ARCHIVE_CODE_PAGE = "default_archive_code_page";
    private const string KEY_ANTI_ALIASING_ENABLED = "anti_aliasing_enabled";
    private const string KEY_READING_COMIC_ID = "reading_comic_id";
    private const string KEY_SAVE_BROWSING_HISTORY = "save_browsing_history";
    private const string KEY_TRANSITION_ANIMATION = "transition_animation";

    private static readonly ConcurrentDictionary<string, ComicModel> sComicMap = new();
    private static int _nextComicToken = 0;

    public static int DefaultArchiveCodePage
    {
        get
        {
            return (int)KVDatabase.GetDefaultMethod().GetLong(DatabaseEntry.KV_DB_APP, KEY_DEFAULT_ARCHIVE_CODE_PAGE, -1);
        }
        set
        {
            KVDatabase.GetDefaultMethod().SetLong(DatabaseEntry.KV_DB_APP, KEY_DEFAULT_ARCHIVE_CODE_PAGE, value);
        }
    }

    public static bool AntiAliasingEnabled
    {
        get
        {
            return KVDatabase.GetDefaultMethod().GetBoolean(DatabaseEntry.KV_DB_APP, KEY_ANTI_ALIASING_ENABLED, false);
        }
        set
        {
            KVDatabase.GetDefaultMethod().SetBoolean(DatabaseEntry.KV_DB_APP, KEY_ANTI_ALIASING_ENABLED, value);
        }
    }

    public static bool SaveBrowsingHistory
    {
        get
        {
            return KVDatabase.GetDefaultMethod().GetBoolean(DatabaseEntry.KV_DB_APP, KEY_SAVE_BROWSING_HISTORY, true);
        }
        set
        {
            KVDatabase.GetDefaultMethod().SetBoolean(DatabaseEntry.KV_DB_APP, KEY_SAVE_BROWSING_HISTORY, value);
        }
    }

    public static bool TransitionAnimation
    {
        get
        {
            return KVDatabase.GetDefaultMethod().GetBoolean(DatabaseEntry.KV_DB_APP, KEY_TRANSITION_ANIMATION, true);
        }
        set
        {
            KVDatabase.GetDefaultMethod().SetBoolean(DatabaseEntry.KV_DB_APP, KEY_TRANSITION_ANIMATION, value);
        }
    }

    public static void SetReadingComic(long id)
    {
        Logger.I(TAG, $"SetReadingComic(id={id})");
        Logger.Assert(id >= 0, "A93DA0E76912639F");
        KVDatabase.GetDefaultMethod().SetLong(DatabaseEntry.KV_DB_APP, KEY_READING_COMIC_ID, id);
    }

    public static void UnsetReadingComic()
    {
        Logger.I(TAG, "UnsetReadingComic");
        KVDatabase.GetDefaultMethod().Remove(DatabaseEntry.KV_DB_APP, KEY_READING_COMIC_ID);
    }

    public static long GetReadingComic()
    {
        long id = KVDatabase.GetDefaultMethod().GetLong(DatabaseEntry.KV_DB_APP, KEY_READING_COMIC_ID, -1);
        Logger.I(TAG, $"GetReadingComic(id={id})");
        return id;
    }

    public static ComicModel GetComicData(string token)
    {
        return sComicMap.TryGetValue(token, out ComicModel comicData) ? comicData : null;
    }

    public static string PutComicData(ComicModel comicData)
    {
        string token = (Interlocked.Increment(ref _nextComicToken) - 1).ToString();
        sComicMap[token] = comicData;
        return token;
    }
}
