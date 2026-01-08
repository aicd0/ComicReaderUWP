// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.ComponentModel;

using ComicReader.Common.Localization;
using ComicReader.Data.Models.Misc;
using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.Views.Pages.Settings;

internal partial class GeneralSettingsViewModel : INotifyPropertyChanged
{
    private const string TAG = nameof(GeneralSettingsViewModel);

    public event PropertyChangedEventHandler? PropertyChanged;

    private List<CloseLastTabBehaviorEntry> _closeLastTabBehaviors = [];
    public List<CloseLastTabBehaviorEntry> CloseLastTabBehaviors
    {
        get => _closeLastTabBehaviors;
        set
        {
            _closeLastTabBehaviors = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CloseLastTabBehaviors)));
        }
    }

    private int _closeLastTabBehaviorIndex = 0;
    public int CloseLastTabBehaviorIndex
    {
        get => _closeLastTabBehaviorIndex;
        set
        {
            _closeLastTabBehaviorIndex = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CloseLastTabBehaviorIndex)));
        }
    }

    private List<TapComicBehaviorEntry> _openComicDefaultBaheviors = [];
    public List<TapComicBehaviorEntry> OpenComicDefaultBaheviors
    {
        get => _openComicDefaultBaheviors;
        set
        {
            _openComicDefaultBaheviors = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OpenComicDefaultBaheviors)));
        }
    }

    private int _openComicDefaultBaheviorIndex = 0;
    public int OpenComicDefaultBaheviorIndex
    {
        get => _openComicDefaultBaheviorIndex;
        set
        {
            _openComicDefaultBaheviorIndex = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OpenComicDefaultBaheviorIndex)));
        }
    }

    public void Initialize()
    {
        UpdateCloseLastTabBehavior();
        UpdateHomePageTapComicBehavior();
    }

    public void SetCloseLastTabBehavior(int index)
    {
        if (index == _closeLastTabBehaviorIndex)
        {
            return;
        }

        if (index < 0 || index >= _closeLastTabBehaviors.Count)
        {
            Logger.F(TAG, "SetCloseLastTabBehavior: Index out of bounds.");
            return;
        }

        _closeLastTabBehaviorIndex = index;
        AppSettingsModel.Instance.CloseLastTabBehavior = _closeLastTabBehaviors[index].Behavior;
    }

    public void SetOpenComicDefaultBehavior(int index)
    {
        if (index == _openComicDefaultBaheviorIndex)
        {
            return;
        }

        if (index < 0 || index >= _openComicDefaultBaheviors.Count)
        {
            Logger.F(TAG, "SetOpenComicDefaultBehavior: Index out of bounds.");
            return;
        }

        _openComicDefaultBaheviorIndex = index;
        AppSettingsModel.Instance.OpenComicDefaultBehavior = _openComicDefaultBaheviors[index].Behavior;
    }

    private void UpdateCloseLastTabBehavior()
    {
        AppSettingsModel.CloseLastTabBehaviorEnum behavior = AppSettingsModel.Instance.CloseLastTabBehavior;
        List<CloseLastTabBehaviorEntry> entries = [
            new(AppSettingsModel.CloseLastTabBehaviorEnum.CloseWindow),
            new(AppSettingsModel.CloseLastTabBehaviorEnum.OpenHomePage),
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

        CloseLastTabBehaviors = entries;
        CloseLastTabBehaviorIndex = selectedIndex;
    }

    private void UpdateHomePageTapComicBehavior()
    {
        AppSettingsModel.OpenComicBehaviorEnum behavior = AppSettingsModel.Instance.OpenComicDefaultBehavior;
        List<TapComicBehaviorEntry> entries = [
            new(AppSettingsModel.OpenComicBehaviorEnum.OpenInCurrentTab),
            new(AppSettingsModel.OpenComicBehaviorEnum.OpenInNewTab),
            new(AppSettingsModel.OpenComicBehaviorEnum.OpenInLastActiveReaderTab),
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

        OpenComicDefaultBaheviors = entries;
        OpenComicDefaultBaheviorIndex = selectedIndex;
    }

    //
    // Types
    //

    public class CloseLastTabBehaviorEntry(AppSettingsModel.CloseLastTabBehaviorEnum behavior)
    {
        public AppSettingsModel.CloseLastTabBehaviorEnum Behavior => behavior;

        public string Name
        {
            get
            {
                return behavior switch
                {
                    AppSettingsModel.CloseLastTabBehaviorEnum.CloseWindow => StringResourceProvider.Instance.CloseWindow,
                    AppSettingsModel.CloseLastTabBehaviorEnum.OpenHomePage => StringResourceProvider.Instance.OpenHomePage,
                    _ => throw new InvalidEnumArgumentException(nameof(Behavior)),
                };
            }
        }
    }

    public class TapComicBehaviorEntry(AppSettingsModel.OpenComicBehaviorEnum behavior)
    {
        public AppSettingsModel.OpenComicBehaviorEnum Behavior => behavior;

        public string Name
        {
            get
            {
                return behavior switch
                {
                    AppSettingsModel.OpenComicBehaviorEnum.OpenInCurrentTab => StringResourceProvider.Instance.OpenInCurrentTab,
                    AppSettingsModel.OpenComicBehaviorEnum.OpenInNewTab => StringResourceProvider.Instance.OpenInNewTab,
                    AppSettingsModel.OpenComicBehaviorEnum.OpenInLastActiveReaderTab => StringResourceProvider.Instance.OpenInLastActiveReaderTab,
                    _ => throw new InvalidEnumArgumentException(nameof(Behavior)),
                };
            }
        }
    }
}
