// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;

using ComicReaderUWP.Common.Actions;

namespace ComicReaderUWP.Views.Pages.Settings;

internal partial class SettingsSharedViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public delegate void UpdateStartedEventHandler();
    public event UpdateStartedEventHandler? UpdateStarted;

    private bool _debugMode;
    public bool DebugMode
    {
        get => _debugMode;
        set
        {
            _debugMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DebugMode)));
        }
    }

    public int WindowId { get; set; }
    public ActionHandler ActionHandler { get; set; } = ActionHandler.Dummy;

    public void Update()
    {
        UpdateStarted?.Invoke();
    }
}
