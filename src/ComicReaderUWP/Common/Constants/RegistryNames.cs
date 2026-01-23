// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Common.Constants;

internal static class RegistryNames
{
    //
    // Main Registry
    //

    public const string GLOBAL = "/Global/";
    public const string SEARCH_HISTORY = $"{GLOBAL}SearchHistory/";
    public const string RESOURCES = "/Resources/";
    public const string RUNTIME_RESOURCES = $"{RESOURCES}Runtime/";
    public const string PLAYLISTS = $"{RUNTIME_RESOURCES}Playlists/";
    public const string TAB_RESOURCES = $"{RESOURCES}Tabs/";
}
