// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Data.Models.Playback;

internal enum PlaybackStateChangeReason
{
    Other,
    Refresh,
    Next,
    Previous,
    PreviousByOverScroll,
}