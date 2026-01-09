// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Plugins.Menu;

public class SubItemMenuItem : IMenuItem
{
    public required string Text { get; set; }
    public string? Glyph { get; set; }
    public List<IMenuItem> Items { get; set; } = [];
}
