// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.KVStorage;

public static class KVDatabase
{
    private static readonly KVDatabaseMethod sDefaultMethod = new KVDatabaseMethodCache(new KVDatabaseMethodLiteDB("lib"));
    private static readonly KVDatabaseMethod sSdkMethod = new KVDatabaseMethodCache(new KVDatabaseMethodLiteDB("sdk"));

    public static KVDatabaseMethod Default => sDefaultMethod;
    internal static KVDatabaseMethod Sdk => sSdkMethod;

    public static void Dispose()
    {
        sDefaultMethod.Dispose();
        sSdkMethod.Dispose();
    }
}
