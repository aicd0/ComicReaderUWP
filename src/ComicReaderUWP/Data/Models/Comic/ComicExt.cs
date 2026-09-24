// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.Storage;

namespace ComicReaderUWP.Data.Models.Comic;

internal static class ComicExt
{
    public const string BACKGROUND_IMAGE = "BackgroundImage";
    public const string COVER_IMAGE = "CoverImage";
    public const string COVER_INDEX = "CoverIndex";
    public const string CUSTOM_READER_SETTINGS = "CustomReaderSettings";
    public const string LINKS = "Links";
    public const string READER_SETTING_PRESET_KEY = "ReaderSettingPresetKey";
    public const string RESOURCE_UUID = "ResourceUuid";

    public static int GetCoverIndex(ComicModel comic)
    {
        string? coverIndexString = comic.GetExt(COVER_INDEX);
        if (!string.IsNullOrEmpty(coverIndexString) && int.TryParse(coverIndexString, out int coverIndex) && coverIndex >= 0)
        {
            return coverIndex;
        }

        string? coverImage = comic.GetExt(COVER_IMAGE);
        if (string.IsNullOrEmpty(coverImage))
        {
            return 0;
        }

        return -1;
    }

    public static string? GetCoverImageUri(ComicModel comic)
    {
        int coverIndex = GetCoverIndex(comic);
        if (coverIndex < 0)
        {
            return comic.GetExt(COVER_IMAGE);
        }

        return comic.IsExternal ? null : ResourceUri.CreateComicImage(comic.Id, coverIndex).ToString();
    }
}
