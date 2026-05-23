// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace ComicReaderUWP.Core.Database.KV;

public interface IKVCollection
{
    bool TryGet<T>(string key, [NotNullWhen(true)] out T? value);

    void Set<T>(string key, T? value);
}
