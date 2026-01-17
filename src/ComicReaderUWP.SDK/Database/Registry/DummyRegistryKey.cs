// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace ComicReaderUWP.SDK.Database.Registry;

public class DummyRegistryKey : IRegistryKey
{
    public static readonly DummyRegistryKey Instance = new();

    private DummyRegistryKey() { }

    public IEnumerable<string> Keys => [];

    public bool Remove(string key)
    {
        return false;
    }

    public void Set<T>(string key, T value)
    {
    }

    public bool TryGet<T>(string key, [NotNullWhen(true)] out T? value)
    {
        value = default;
        return false;
    }
}
