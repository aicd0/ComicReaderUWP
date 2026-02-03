// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.ComponentModel;

using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.SDK.Common.Utils;

namespace ComicReaderUWP.Views.Pages.Settings;

internal partial class ReaderSettingsViewModel : INotifyPropertyChanged
{
    private const string TAG = nameof(ReaderSettingsViewModel);

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

    private bool _transitionAnimation = true;
    public bool TransitionAnimation
    {
        get => _transitionAnimation;
        set
        {
            _transitionAnimation = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TransitionAnimation)));

            AppSettingsModel.Instance.TransitionAnimation = value;
        }
    }

    private bool _antiAliasingEnabled = true;
    public bool AntiAliasingEnabled
    {
        get => _antiAliasingEnabled;
        set
        {
            _antiAliasingEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AntiAliasingEnabled)));

            AppSettingsModel.Instance.AntiAliasingEnabled = value;
        }
    }

    private bool _restoreLastReadingPosition = true;
    public bool RestoreLastReadingPosition
    {
        get => _restoreLastReadingPosition;
        set
        {
            _restoreLastReadingPosition = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RestoreLastReadingPosition)));
        }
    }

    private bool _automaticallyHideCursor = true;
    public bool AutomaticallyHideCursor
    {
        get => _automaticallyHideCursor;
        set
        {
            _automaticallyHideCursor = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutomaticallyHideCursor)));
        }
    }

    private List<KeepScreenOnBehaviorEntry> _keepScreenOnBehaviors = [];
    public List<KeepScreenOnBehaviorEntry> KeepScreenOnBehaviors
    {
        get => _keepScreenOnBehaviors;
        set
        {
            _keepScreenOnBehaviors = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(KeepScreenOnBehaviors)));
        }
    }

    private int _keepScreenOnBehaviorIndex = 0;
    public int KeepScreenOnBehaviorIndex
    {
        get => _keepScreenOnBehaviorIndex;
        set
        {
            _keepScreenOnBehaviorIndex = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(KeepScreenOnBehaviorIndex)));
        }
    }

    public void Initialize(SettingsSharedViewModel shared)
    {
        Shared = shared;
        Shared.UpdateStarted += Update;
    }

    public void SetRestoreLastReadingPosition(bool restoreLastReadingPosition)
    {
        _restoreLastReadingPosition = restoreLastReadingPosition;
        AppSettingsModel.Instance.RestoreLastReadingPosition = restoreLastReadingPosition;
    }

    public void SetHideCursorAutomatically(bool hideCursorAutomatically)
    {
        _automaticallyHideCursor = hideCursorAutomatically;
        AppSettingsModel.Instance.AutomaticallyHideCursor = hideCursorAutomatically;
    }

    public void SetKeepScreenOnBehavior(int index)
    {
        if (index == _keepScreenOnBehaviorIndex || index < 0 || index >= _keepScreenOnBehaviors.Count)
        {
            return;
        }

        _keepScreenOnBehaviorIndex = index;
        AppSettingsModel.Instance.KeepScreenOnBehavior = _keepScreenOnBehaviors[index].Behavior;
    }

    private void Update()
    {
        UpdateBasicReaderSettings();
        UpdateKeepScreenOnBehavior();
    }

    private void UpdateBasicReaderSettings()
    {
        CoroutineUtils.RunInMainThread(() =>
        {
            TransitionAnimation = AppSettingsModel.Instance.TransitionAnimation;
            AntiAliasingEnabled = AppSettingsModel.Instance.AntiAliasingEnabled;
            RestoreLastReadingPosition = AppSettingsModel.Instance.RestoreLastReadingPosition;
            AutomaticallyHideCursor = AppSettingsModel.Instance.AutomaticallyHideCursor;
        });
    }

    private void UpdateKeepScreenOnBehavior()
    {
        AppSettingsModel.KeepScreenOnBehaviorEnum behavior = AppSettingsModel.Instance.KeepScreenOnBehavior;
        List<KeepScreenOnBehaviorEntry> entries = [
            new(AppSettingsModel.KeepScreenOnBehaviorEnum.Never),
            new(AppSettingsModel.KeepScreenOnBehaviorEnum.Always),
            new(AppSettingsModel.KeepScreenOnBehaviorEnum.DuringAutoScrolling),
        ];
        int selectedIndex = -1;
        for (int i = 0; i < entries.Count; i++)
        {
            if (behavior == entries[i].Behavior)
            {
                selectedIndex = i;
                break;
            }
        }

        if (selectedIndex < 0)
        {
            selectedIndex = 0;
        }

        KeepScreenOnBehaviors = entries;
        KeepScreenOnBehaviorIndex = selectedIndex;
    }

    //
    // Types
    //

    public class KeepScreenOnBehaviorEntry(AppSettingsModel.KeepScreenOnBehaviorEnum behavior)
    {
        public AppSettingsModel.KeepScreenOnBehaviorEnum Behavior => behavior;

        public string Name
        {
            get
            {
                return behavior switch
                {
                    AppSettingsModel.KeepScreenOnBehaviorEnum.Never => StringResourceProvider.Instance.Never,
                    AppSettingsModel.KeepScreenOnBehaviorEnum.Always => StringResourceProvider.Instance.Always,
                    AppSettingsModel.KeepScreenOnBehaviorEnum.DuringAutoScrolling => StringResourceProvider.Instance.DuringAutoScrolling,
                    _ => throw new InvalidEnumArgumentException(nameof(Behavior)),
                };
            }
        }
    }
}
