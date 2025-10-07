// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ComicReader.Common.Utils;

class StringUtils
{
    public class OrdinalComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            return string.CompareOrdinal(x, y);
        }
    }

    private class SmartFileNameComparerInternal : IComparer<List<string>>
    {
        public int Compare(List<string>? x, List<string>? y)
        {
            if (x == null && y == null)
            {
                return 0;
            }

            if (x == null)
            {
                return -1;
            }

            if (y == null)
            {
                return 1;
            }

            for (int i = 0; i < Math.Min(x.Count, y.Count); i++)
            {
                if (int.TryParse(x[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int xint) &&
                    int.TryParse(y[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int yint))
                {
                    if (xint != yint)
                    {
                        return xint - yint;
                    }
                }

                int ordinal = string.Compare(x[i], y[i], ignoreCase: true);
                if (ordinal != 0)
                {
                    return ordinal;
                }
            }

            return x.Count - y.Count;
        }
    }

    public static IComparer<List<string>> SmartFileNameComparer { get; } = new SmartFileNameComparerInternal();

    public static Func<string, List<string>> SmartFileNameKeySelector { get; } = delegate (string x)
    {
        List<string> list = [];
        bool lastIsNumber = false;
        int startIndex = 0;
        for (int i = 0; i < x.Length; i++)
        {
            char c = x[i];
            bool isNumber = c >= '0' && c <= '9';
            if (isNumber != lastIsNumber)
            {
                if (startIndex < i)
                {
                    list.Add(x[startIndex..i]);
                    startIndex = i;
                }

                lastIsNumber = isNumber;
            }
        }

        if (x.Length > 0)
        {
            list.Add(x[startIndex..]);
        }

        return list;
    };

    public static string Join(string seperator, IEnumerable<string> list)
    {
        if (!list.Any())
        {
            return "";
        }
        StringBuilder res = new(list.First());
        bool first = true;
        foreach (string s in list)
        {
            if (first)
            {
                first = false;
                continue;
            }
            res.Append(seperator).Append(s);
        }
        return res.ToString();
    }

    public static int QuickMatch(List<string> keywords, string str)
    {
        int totalSimilarity = 0;
        List<int> keywordIndices = new(8);
        foreach (string keyword in keywords)
        {
            if (keyword.Length == 0)
            {
                continue;
            }

            keywordIndices.Clear();
            int similarity = 0;
            foreach (char c in str)
            {
                for (int i = keywordIndices.Count - 1; i >= 0; i--)
                {
                    int keywordIndex = keywordIndices[i];
                    if (keyword[keywordIndex] == c)
                    {
                        keywordIndex++;
                        if (keywordIndex >= keyword.Length)
                        {
                            ++similarity;
                            keywordIndices.RemoveAt(i);
                        }
                        else
                        {
                            keywordIndices[i] = keywordIndex;
                        }
                    }
                    else
                    {
                        keywordIndices.RemoveAt(i);
                    }
                }

                if (keyword[0] == c)
                {
                    if (1 >= keyword.Length)
                    {
                        ++similarity;
                    }
                    else
                    {
                        keywordIndices.Add(1);
                    }
                }
            }

            if (similarity == 0)
            {
                return 0;
            }

            totalSimilarity += similarity;
        }

        return totalSimilarity;
    }

    public static string UniquePath(string path)
    {
        return path.ToLower();
    }

    public static bool IsBeginWith(string text, string subText)
    {
        if (subText.Length > text.Length)
        {
            return false;
        }
        return text[..subText.Length].Equals(subText);
    }

    public static bool FolderContain(string parentPath, string childPath)
    {
        parentPath = ToFolderPath(UniquePath(parentPath));
        childPath = ToFolderPath(UniquePath(childPath));
        if (parentPath.Length > childPath.Length)
        {
            return false;
        }
        return childPath[..parentPath.Length].Equals(parentPath);
    }

    public static string ToFolderPath(string path)
    {
        path = path.Replace('/', '\\');
        if (path.Length == 0)
        {
            return path;
        }
        if (path[^1] != '\\')
        {
            path += '\\';
        }
        return path;
    }

    public static string ItemNameFromPath(string path)
    {
        int i = path.LastIndexOf('\\');
        if (i == -1)
        {
            return path;
        }
        return path[(i + 1)..];
    }

    public static string ParentLocationFromLocation(string location)
    {
        int i = location.LastIndexOf('\\');
        if (i == -1)
        {
            return "";
        }
        if (i > 0 && location[i - 1] == '\\')
        {
            i--;
        }
        return location[..i];

    }

    public static string ExtensionFromFilename(string filename)
    {
        int i = filename.LastIndexOf('.');
        if (i == -1)
        {
            return "";
        }
        return filename[i..];
    }

    public static string DisplayNameFromFilename(string filename)
    {
        int i = filename.LastIndexOf('.');
        if (i == -1)
        {
            return filename;
        }
        return filename[..i];
    }

    public static string ToPathNoTail(string path)
    {
        if (path.Length == 0)
        {
            return path;
        }
        if (path[^1] == '\\')
        {
            return path[..^1];
        }
        return path;
    }

    public static string RandomFileName(int length)
    {
        const string symbols = "0123456789abcdefghijklmnopqrstuvwxyz";
        StringBuilder sb = new();
        for (int i = 0; i < length; ++i)
        {
            sb.Append(symbols[Random.Shared.Next(symbols.Length)]);
        }
        return sb.ToString();
    }

    public static int ParseInt(string text, int defaultValue = 0)
    {
        if (int.TryParse(text, out int result))
        {
            return result;
        }

        return defaultValue;
    }

    public static bool TryNormalizeWebUrl(string url, out Uri? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        // Try to parse as absolute URI
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? absoluteUri) &&
            (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
        {
            result = absoluteUri;
            return true;
        }

        // If it looks like a domain or starts with www, prepend https://
        if (url.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ||
            (url.Contains('.') && !url.Contains(' ')))
        {
            string candidate = "https://" + url.Trim();
            if (Uri.TryCreate(candidate, UriKind.Absolute, out Uri? webUri))
            {
                result = webUri;
                return true;
            }
        }

        // Not a valid web url
        return false;
    }
}
