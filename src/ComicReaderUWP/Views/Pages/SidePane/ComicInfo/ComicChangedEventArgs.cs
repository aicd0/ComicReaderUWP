// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;

namespace ComicReaderUWP.Views.Pages.SidePane.ComicInfo;

internal class ComicChangedEventArgs
{
    public required ComicModel? Comic;
    public required PlaylistModel Playlist;
    public required int PageIndex;
}
