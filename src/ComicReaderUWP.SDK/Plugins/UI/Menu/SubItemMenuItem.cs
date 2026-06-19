// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.SDK.Plugins.UI.Menu;

public sealed class SubItemMenuItem : IMenuItem
{
    public required string Text { get; set; }
    public IconSource? Icon { get; set; }
    public List<IMenuItem> Items { get; set; } = [];
}
