// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ComicReader.Common.BaseUI;

internal class PageBundle(IReadOnlyDictionary<string, string> parameters)
{
    private readonly IReadOnlyDictionary<string, string> _parameters = parameters;

    [return: NotNullIfNotNull(nameof(defaultValue))]
    public string? GetString(string key, string? defaultValue = null)
    {
        if (_parameters.TryGetValue(key, out string? value))
        {
            return value;
        }

        return defaultValue;
    }
}
