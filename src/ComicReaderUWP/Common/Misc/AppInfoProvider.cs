// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace ComicReaderUWP.Common.Misc;

public class AppInfoProvider
{
    private static ReadOnlyDictionary<int, Encoding> _supportedEncodings = null;

    public static readonly HashSet<string> SupportedImageExtensions =
    [
        ".avif",
        ".bmp",
        ".gif",
        ".heic",
        ".heif",
        ".jpg",
        ".jpe",
        ".jpeg",
        ".png",
        ".tif",
        ".tiff",
        ".webp",
    ];

    public static readonly HashSet<string> SupportedArchiveExtensions =
    [
        ".7z",
        ".bz2",
        ".cb7",
        ".cbr",
        ".cbt",
        ".cbz",
        ".gz",
        ".rar",
        ".tar",
        ".xz",
        ".zip",
    ];

    public static readonly HashSet<string> SupportedDocumentExtensions =
    [
        ".pdf",
    ];

    private static HashSet<string> _supportedExternalFileExtensions = null;
    public static HashSet<string> SupportedExternalFileExtensions
    {
        get
        {
            if (_supportedExternalFileExtensions == null)
            {
                _supportedExternalFileExtensions = [];
                _supportedExternalFileExtensions.UnionWith(SupportedImageExtensions);
                _supportedExternalFileExtensions.UnionWith(SupportedArchiveExtensions);
                _supportedExternalFileExtensions.UnionWith(SupportedDocumentExtensions);
            }

            return _supportedExternalFileExtensions;
        }
    }

    public static bool IsSupportedExternalFileExtension(string extension)
    {
        return SupportedExternalFileExtensions.Contains(extension.ToLower());
    }

    public static bool IsSupportedImageExtension(string extension)
    {
        return SupportedImageExtensions.Contains(extension.ToLower());
    }

    public static bool IsSupportedArchiveExtension(string extension)
    {
        return SupportedArchiveExtensions.Contains(extension.ToLower());
    }

    public static bool IsSupportedDocumentExtension(string extension)
    {
        return SupportedDocumentExtensions.Contains(extension.ToLower());
    }

    public static ReadOnlyDictionary<int, Encoding> GetSupportedEncodings()
    {
        if (_supportedEncodings != null)
        {
            return _supportedEncodings;
        }

        // Encoding.GetEncodings() doesn't return the full list of encodings.
        // At least it doesn't work for current version of UWP.
        // ref: https://github.com/dotnet/runtime/issues/25819
        // So we hard-coded the whole list instead.
        // ref: https://docs.microsoft.com/en-us/windows/win32/intl/code-page-identifiers
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        int[] codepages = [
            37, 437, 500, 708, 709, 710, 720, 737, 775, 850, 852, 855, 857, 858,
            860, 861, 862, 863, 864, 865, 866, 869, 870, 874, 875, 932, 936, 949,
            950, 1026, 1047, 1140, 1141, 1142, 1143, 1144, 1145, 1146, 1147, 1148,
            1149, 1200, 1201, 1250, 1251, 1252, 1253, 1254, 1255, 1256, 1257, 1258,
            1361, 10000, 10001, 10002, 10003, 10004, 10005, 10006, 10007, 10008,
            10010, 10017, 10021, 10029, 10079, 10081, 10082, 12000, 12001, 20000,
            20001, 20002, 20003, 20004, 20005, 20105, 20106, 20107, 20108, 20127,
            20261, 20269, 20273, 20277, 20278, 20280, 20284, 20285, 20290, 20297,
            20420, 20423, 20424, 20833, 20838, 20866, 20871, 20880, 20905, 20924,
            20932, 20936, 20949, 21025, 21027, 21866, 28591, 28592, 28593, 28594,
            28595, 28596, 28597, 28598, 28599, 28603, 28605, 29001, 38598, 50220,
            50221, 50222, 50225, 50227, 50229, 50930, 50931, 50933, 50935, 50936,
            50937, 50939, 51932, 51936, 51949, 51950, 52936, 54936, 57002, 57003,
            57004, 57005, 57006, 57007, 57008, 57009, 57010, 57011, 65000, 65001,
        ];
        var supportedEncodings = new Dictionary<int, Encoding>();
        foreach (int codepage in codepages)
        {
            try
            {
                var info = Encoding.GetEncoding(codepage);
                supportedEncodings.Add(codepage, info);
            }
            catch { }
        }
        _supportedEncodings = new ReadOnlyDictionary<int, Encoding>(supportedEncodings);
        return _supportedEncodings;
    }
}
