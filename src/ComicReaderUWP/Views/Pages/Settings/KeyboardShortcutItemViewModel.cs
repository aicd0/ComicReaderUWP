// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.HotKey;

namespace ComicReaderUWP.Views.Pages.Settings;

internal sealed class KeyboardShortcutItemViewModel(KeyboardShortcutModel model) : BaseViewModel
{
    public KeyboardShortcutModel Model { get; private set; } = model;

    public string ShortcutText => Model.ToString();

    public string ActionName => KeyboardShortcutActions.Get(Model.Action)?.Name ?? Model.Action;

    public void Update(KeyboardShortcutModel model)
    {
        Model = model;
    }
}
