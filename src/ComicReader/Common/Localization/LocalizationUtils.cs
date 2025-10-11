// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Common.Localization;

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
}
