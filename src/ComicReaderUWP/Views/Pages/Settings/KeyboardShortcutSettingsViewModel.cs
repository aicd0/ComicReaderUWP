// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

using ComicReaderUWP.Common.HotKey;
using ComicReaderUWP.Core.Common.Algorithm;

namespace ComicReaderUWP.Views.Pages.Settings;

internal partial class KeyboardShortcutSettingsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private SettingsSharedViewModel _shared = new();
    public SettingsSharedViewModel Shared
    {
        get => _shared;
        private set
        {
            _shared = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Shared)));
        }
    }

    public ObservableCollection<KeyboardShortcutItemViewModel> Shortcuts { get; } = [];

    public bool HasShortcuts => Shortcuts.Count > 0;

    public void Initialize(SettingsSharedViewModel shared)
    {
        Shared = shared;
        Refresh();
    }

    public void Refresh()
    {
        List<KeyboardShortcutItemViewModel> items = [];
        foreach (KeyboardShortcutModel shortcut in KeyboardShortcutManager.GetShortcuts())
        {
            items.Add(new KeyboardShortcutItemViewModel(shortcut));
        }

        items.Sort((a, b) => string.Compare(a.ShortcutText, b.ShortcutText, StringComparison.Ordinal));

        DiffUtils.UpdateCollection(Shortcuts, items,
            (a, b) => a.Model.Action == b.Model.Action && a.Model.HasSameKeys(b.Model),
            (a, b) => a.Update(b.Model));

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasShortcuts)));
    }

    public void Add(KeyboardShortcutModel shortcut)
    {
        KeyboardShortcutManager.Add(shortcut);
    }

    public void Update(KeyboardShortcutModel original, KeyboardShortcutModel updated)
    {
        KeyboardShortcutManager.Update(original, updated);
    }

    public void Remove(KeyboardShortcutModel shortcut)
    {
        KeyboardShortcutManager.Remove(shortcut);
    }
}
