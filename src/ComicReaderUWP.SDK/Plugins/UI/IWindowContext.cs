// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.SDK.Models;
using ComicReaderUWP.SDK.Plugins.Comic;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.SDK.Plugins.UI;

public delegate void ReadingComicChangedEventHandler(IComicModel? comic);

public interface IWindowContext
{
    event ReadingComicChangedEventHandler? ReadingComicChanged;

    IComicModel? ReadingComic { get; }

    Task<DialogResult> EnqueueDialog(DialogOptions options);

    Task<DialogResult> EnqueueDialog(ContentDialog dialog);
}
