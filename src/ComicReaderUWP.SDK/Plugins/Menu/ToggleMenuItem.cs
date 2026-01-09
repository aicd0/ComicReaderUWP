// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Plugins.Menu;

public class ToggleMenuItem : IMenuItem
{
    public required string Text { get; set; }
    public bool IsChecked { get; set; } = false;
    public Action? Click { get; set; }
}
