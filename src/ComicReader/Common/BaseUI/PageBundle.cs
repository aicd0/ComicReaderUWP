// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;

namespace ComicReader.Common.BaseUI;

internal class PageBundle(Dictionary<string, string> parameters)
{
    private readonly Dictionary<string, string> _parameters = parameters;

    public string GetString(string key, string defaultValue = "")
    {
        if (_parameters.TryGetValue(key, out string value))
        {
            return value;
        }

        return defaultValue;
    }
}
