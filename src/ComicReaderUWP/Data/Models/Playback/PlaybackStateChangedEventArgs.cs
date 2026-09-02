// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Data.Models.Playback;

internal class PlaybackStateChangedEventArgs
{
    public required PlaybackStateChangeReason Reason { get; init; }
    public required double InitialPage { get; init; }
}
