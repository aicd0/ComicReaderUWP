// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.KVStorage;

public static class KVDatabase
{
    private static readonly Lazy<KVDatabaseMethod> sDefaultMethod = new(delegate
    {
        return new KVDatabaseMethodCache(new KVDatabaseMethodLiteDB("lib"));
    });

    private static readonly Lazy<KVDatabaseMethod> sSdkMethod = new(delegate
    {
        return new KVDatabaseMethodCache(new KVDatabaseMethodLiteDB("sdk"));
    });

    public static KVDatabaseMethod Default => sDefaultMethod.Value;
    internal static KVDatabaseMethod Sdk => sSdkMethod.Value;
}
