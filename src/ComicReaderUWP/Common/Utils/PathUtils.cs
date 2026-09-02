// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace ComicReaderUWP.Common.Utils;

internal static class PathUtils
{
    public static bool TryNormalizePath(string path, [NotNullWhen(true)] out string? result)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            result = null;
            return false;
        }

        try
        {
            result = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            result = null;
            return false;
        }

        result = result.Replace('/', '\\');
        return true;
    }

    public static bool IsPathEquivalent(string path1, string path2)
    {
        if (!TryNormalizePath(path1, out string? normalizedPath1) || !TryNormalizePath(path2, out string? normalizedPath2))
        {
            return false;
        }

        return string.Equals(normalizedPath1, normalizedPath2, StringComparison.OrdinalIgnoreCase);
    }
}
