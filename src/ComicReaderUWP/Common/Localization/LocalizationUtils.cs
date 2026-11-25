// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace ComicReaderUWP.Common.Localization;

internal static class LocalizationUtils
{
    public static char[] Commas { get; } = [
        '\u002c', // ,
        '\uff0c', // ，
        '\u3001', // 、
    ];

    public static char[] Colons { get; } = [
        '\u003a', // :
        '\uff1a', // ：
    ];

    /// <summary>
    /// Picks the candidate tag that best matches the given culture: exact name, then the same language with the same
    /// script, then the same language with the same region, then the same language alone.
    /// </summary>
    public static string? ResolveLanguage(CultureInfo culture, IEnumerable<string> candidates)
    {
        string? best = null;
        int bestScore = 0;
        foreach (string candidate in candidates)
        {
            int score = GetLanguageMatchScore(culture, candidate);
            if (score > bestScore || (score == bestScore && best is not null && string.CompareOrdinal(candidate, best) < 0))
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    private static int GetLanguageMatchScore(CultureInfo culture, string languageTag)
    {
        CultureInfo candidate;
        try
        {
            candidate = new CultureInfo(languageTag);
        }
        catch (CultureNotFoundException)
        {
            return 0;
        }

        if (string.Equals(culture.Name, candidate.Name, StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (!string.Equals(culture.TwoLetterISOLanguageName, candidate.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        string cultureScript = GetScriptSubtag(culture);
        string candidateScript = GetScriptSubtag(candidate);
        if (cultureScript.Length > 0 && candidateScript.Length > 0)
        {
            return string.Equals(cultureScript, candidateScript, StringComparison.OrdinalIgnoreCase) ? 3 : 0;
        }

        string cultureRegion = GetRegionSubtag(culture);
        string candidateRegion = GetRegionSubtag(candidate);
        if (cultureRegion.Length > 0 && string.Equals(cultureRegion, candidateRegion, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 1;
    }

    private static string GetScriptSubtag(CultureInfo culture)
    {
        // A script subtag is four letters; cultures that omit it inherit one from their parent (zh-TW is zh-Hant).
        for (CultureInfo? current = culture; current is not null && current.Name.Length > 0; current = current.Parent)
        {
            foreach (string subtag in current.Name.Split('-'))
            {
                if (subtag.Length == 4 && char.IsLetter(subtag[0]))
                {
                    return subtag;
                }
            }
        }

        return string.Empty;
    }

    private static string GetRegionSubtag(CultureInfo culture)
    {
        string[] subtags = culture.Name.Split('-');
        if (subtags.Length < 2)
        {
            return string.Empty;
        }

        string last = subtags[^1];
        bool isRegion = (last.Length == 2 && char.IsLetter(last[0]) && char.IsLetter(last[1])) ||
            (last.Length == 3 && char.IsDigit(last[0]));
        return isRegion ? last : string.Empty;
    }
}
