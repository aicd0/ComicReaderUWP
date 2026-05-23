// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace ComicReaderUWP.SDK.Models;

public interface IRegistryKey
{
    int Count { get; }

    IEnumerable<string> Keys { get; }

    bool TryGet<T>(string key, [NotNullWhen(true)] out T? value);

    void Set<T>(string key, T value);

    bool Remove(string key);
}
