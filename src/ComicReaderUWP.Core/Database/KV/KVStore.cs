// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Database.KV;

public static class KVStore
{
    public static IKVDatabase CreateDatabase(string databasePath, string? legacyName = null)
    {
        IDatabaseLayer? fallbackLayer = null;
        if (!string.IsNullOrEmpty(legacyName))
        {
            fallbackLayer = new OldLiteDBLayer(legacyName);
        }

        return new CacheLayer(new LiteDBLayer(databasePath, fallbackLayer: fallbackLayer));
    }
}
