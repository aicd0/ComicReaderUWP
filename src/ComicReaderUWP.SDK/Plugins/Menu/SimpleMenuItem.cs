// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Plugins.Menu;

public class SimpleMenuItem : IMenuItem
{
    public required string Text { get; set; }
    public string? Glyph { get; set; }
    public bool IsEnabled { get; set; } = true;
    public Action? Click { get; set; }
}
