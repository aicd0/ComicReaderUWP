// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.Storage;
using ComicReaderUWP.Core.Database.KV;
using ComicReaderUWP.Core.Database.Registry;
using ComicReaderUWP.SDK.Models;

namespace ComicReaderUWP.Core.Database.Misc;

public static class CoreDB
{
    private static string RegistryDirectory => Path.Combine(StorageLocation.LocalFolderPath, "reg");
    private static string KvDirectory => Path.Combine(StorageLocation.LocalFolderPath, "kv");

    private static readonly Lazy<IRegistryDatabase> _coreRegistryDatabase = new(() =>
    {
        string databasePath = Path.Combine(RegistryDirectory, "Core.db");
        return RegistryStore.CreateDatabase(databasePath);
    });
    public static IRegistryDatabase CoreRegistry => _coreRegistryDatabase.Value;

    private static readonly Lazy<IKVDatabase> _sdkKvDatabase = new(() =>
    {
        string databasePath = Path.Combine(KvDirectory, $"sdk.db");
        return KVStore.CreateDatabase(databasePath, shared: true);
    });
    internal static IKVDatabase SdkKV => _sdkKvDatabase.Value;

    public static void Dispose()
    {
        if (_coreRegistryDatabase.IsValueCreated)
        {
            _coreRegistryDatabase.Value.Dispose();
        }

        if (_sdkKvDatabase.IsValueCreated)
        {
            _sdkKvDatabase.Value.Dispose();
        }
    }
}
