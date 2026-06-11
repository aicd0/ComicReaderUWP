// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.Storage;
using ComicReaderUWP.Core.Database.KV;

namespace ComicReaderUWP.Core.Database.Misc;

public static class SdkDB
{
    private static string KvDirectory => Path.Combine(StorageLocation.LocalFolderPath, "kv");

    private static readonly Lazy<IKVDatabase> _sdkKvDatabase = new(() =>
    {
        string databasePath = Path.Combine(KvDirectory, $"sdk.db");
        return KVStore.CreateDatabase(databasePath, shared: true);
    });
    internal static IKVDatabase SdkKV => _sdkKvDatabase.Value;

    public static void Dispose()
    {
        if (_sdkKvDatabase.IsValueCreated)
        {
            _sdkKvDatabase.Value.Dispose();
        }
    }
}
