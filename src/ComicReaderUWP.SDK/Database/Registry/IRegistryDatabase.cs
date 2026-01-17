// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace ComicReaderUWP.SDK.Database.Registry;

public interface IRegistryDatabase : IDisposable
{
    IEnumerable<string> GetKeys(string path, bool recursive);

    IRegistryKey CreateKey(string path);

    bool TryGetKey(string path, [NotNullWhen(true)] out IRegistryKey? key);

    bool RemoveKey(string path);
}
