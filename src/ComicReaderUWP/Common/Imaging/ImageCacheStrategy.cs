// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace ComicReaderUWP.Common.Imaging;

internal static class ImageCacheStrategy
{
    private const string CACHE_ENTRY_KEY_100K = "100k";
    private const string CACHE_ENTRY_KEY_300K = "300k";
    private const int CACHE_ENTRY_RESOLUTION_100K = 100000;
    private const int CACHE_ENTRY_RESOLUTION_300K = 300000;

    public static IEnumerable<string> CalculateCacheEntryKeys(int desiredWidth, int desiredHeight, int originWidth, int originHeight)
    {
        List<string> keys = [];

        int desiredResolution = desiredWidth * desiredHeight;
        int originResolution = originWidth * originHeight;

        if (desiredResolution >= originResolution)
        {
            return keys;
        }

        if (desiredResolution <= CACHE_ENTRY_RESOLUTION_100K)
        {
            keys.Add(CACHE_ENTRY_KEY_100K);
        }

        if (desiredResolution <= CACHE_ENTRY_RESOLUTION_300K)
        {
            keys.Add(CACHE_ENTRY_KEY_300K);
        }

        return keys;
    }

    public static int GetCacheResolution(string cacheEntryKey)
    {
        if (string.IsNullOrEmpty(cacheEntryKey))
        {
            return 0;
        }

        return cacheEntryKey switch
        {
            CACHE_ENTRY_KEY_100K => CACHE_ENTRY_RESOLUTION_100K,
            CACHE_ENTRY_KEY_300K => CACHE_ENTRY_RESOLUTION_300K,
            _ => 0,
        };
    }
}
