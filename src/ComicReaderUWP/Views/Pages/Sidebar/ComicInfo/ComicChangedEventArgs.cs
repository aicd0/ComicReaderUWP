// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;

namespace ComicReaderUWP.Views.Pages.Sidebar.ComicInfo;

internal class ComicChangedEventArgs
{
    public required ComicModel? Comic;
    public required PlaylistModel Playlist;
    public required IReadOnlyList<string> ImageDescriptions;
}
