// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Database.Registry;

public static class RegistryStore
{
    public static IRegistryDatabase CreateDatabase(string databasePath)
    {
        databasePath = Path.GetFullPath(databasePath);
        return new LiteDBLayer(databasePath);
    }
}
