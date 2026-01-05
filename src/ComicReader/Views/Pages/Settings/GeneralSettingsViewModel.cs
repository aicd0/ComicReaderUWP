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

    private List<TapComicBehaviorEntry> _homePageTapComicBaheviors = [];
    public List<TapComicBehaviorEntry> HomePageTapComicBaheviors
    {
        get => _homePageTapComicBaheviors;
        set
        {
            _homePageTapComicBaheviors = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HomePageTapComicBaheviors)));
        }
    }

    private int _homePageTapComicBaheviorIndex = 0;
    public int HomePageTapComicBaheviorIndex
    {
        get => _homePageTapComicBaheviorIndex;
        set
        {
            _homePageTapComicBaheviorIndex = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HomePageTapComicBaheviorIndex)));
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

    public void SetHomePageTapComicBehavior(int index)
    {
        if (index == _homePageTapComicBaheviorIndex)
        {
            return;
        }

        if (index < 0 || index >= _homePageTapComicBaheviors.Count)
        {
            Logger.F(TAG, "SetHomePageTapComicBehavior: Index out of bounds.");
            return;
        }

        _homePageTapComicBaheviorIndex = index;
        AppSettingsModel.Instance.HomePageTapComicBehavior = _homePageTapComicBaheviors[index].Behavior;
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
        AppSettingsModel.TapComicBehaviorEnum behavior = AppSettingsModel.Instance.HomePageTapComicBehavior;
        List<TapComicBehaviorEntry> entries = [
            new(AppSettingsModel.TapComicBehaviorEnum.OpenInCurrentTab),
            new(AppSettingsModel.TapComicBehaviorEnum.OpenInNewTab),
            new(AppSettingsModel.TapComicBehaviorEnum.OpenInLastActiveReaderTab),
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

        HomePageTapComicBaheviors = entries;
        HomePageTapComicBaheviorIndex = selectedIndex;
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

    public class TapComicBehaviorEntry(AppSettingsModel.TapComicBehaviorEnum behavior)
    {
        public AppSettingsModel.TapComicBehaviorEnum Behavior => behavior;

        public string Name
        {
            get
            {
                return behavior switch
                {
                    AppSettingsModel.TapComicBehaviorEnum.OpenInCurrentTab => StringResourceProvider.Instance.OpenInCurrentTab,
                    AppSettingsModel.TapComicBehaviorEnum.OpenInNewTab => StringResourceProvider.Instance.OpenInNewTab,
                    AppSettingsModel.TapComicBehaviorEnum.OpenInLastActiveReaderTab => StringResourceProvider.Instance.OpenInLastActiveReaderTab,
                    _ => throw new InvalidEnumArgumentException(nameof(Behavior)),
                };
            }
        }
    }
}
