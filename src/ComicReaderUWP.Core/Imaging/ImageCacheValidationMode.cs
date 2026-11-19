// Copyright (c) aicd0. All rights reserved.
// Licensed under the GNU General Public License v3.0 or later.

namespace ComicReaderUWP.Core.Imaging;

/// <summary>
/// Whether a cached picture is checked against the file it came from before it is handed out.
/// </summary>
public enum ImageCacheValidationMode
{
    /// <summary>
    /// Never checked. Whatever is cached is used, and a file edited afterwards keeps the picture it had.
    /// </summary>
    None,

    /// <summary>
    /// Checked before the cache is used, so a caller waits for the file to be read and an edited file is drawn again
    /// rather than shown as it was.
    /// </summary>
    Synchronized,

    /// <summary>
    /// The cache is handed out first and the file is read afterwards, so nothing waits for it. A file that turns out to
    /// have been edited has its cache dropped once that is known: the picture shown is the one from before the edit, and
    /// the next access draws a new one.
    /// </summary>
    Deferred,
}
