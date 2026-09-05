// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Misc;

namespace ComicReaderUWP.Views.Pages.Settings;

internal partial class GeneralSettingsViewModel : INotifyPropertyChanged
{
    private const string TAG = nameof(GeneralSettingsViewModel);

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

    private bool _isClearHistoryEnabled = false;
    public bool IsClearHistoryEnabled
    {
        get => _isClearHistoryEnabled;
        set
        {
            _isClearHistoryEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsClearHistoryEnabled)));
        }
    }

    private bool _saveBrowsingHistory = false;
    public bool SaveBrowsingHistory
    {
        get => _saveBrowsingHistory;
        set
        {
            _saveBrowsingHistory = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SaveBrowsingHistory)));
        }
    }

    private bool _enableCompressedFileCache = true;
    public bool EnableCompressedFileCache
    {
        get => _enableCompressedFileCache;
        set
        {
            _enableCompressedFileCache = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EnableCompressedFileCache)));
        }
    }

    public void Initialize(SettingsSharedViewModel shared)
    {
        Shared = shared;
        Shared.UpdateStarted += Update;
    }

    public void SetCloseLastTabBehavior(int index)
    {
        if (index == _closeLastTabBehaviorIndex || index < 0 || index >= _closeLastTabBehaviors.Count)
        {
            return;
        }

        _closeLastTabBehaviorIndex = index;
        AppSettingsModel.Instance.CloseLastTabBehavior = _closeLastTabBehaviors[index].Behavior;
    }

    public void SetOpenComicDefaultBehavior(int index)
    {
        if (index == _openComicDefaultBaheviorIndex || index < 0 || index >= _openComicDefaultBaheviors.Count)
        {
            return;
        }

        _openComicDefaultBaheviorIndex = index;
        AppSettingsModel.Instance.OpenComicDefaultBehavior = _openComicDefaultBaheviors[index].Behavior;
    }

    private void Update()
    {
        UpdateCloseLastTabBehavior();
        UpdateHomePageTapComicBehavior();
        UpdateCommonSettings();
        CoroutineUtils.Run(UpdateHistory);
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

    private void UpdateCommonSettings()
    {
        EnableCompressedFileCache = AppSettingsModel.Instance.EnableCompressedFileCache;
    }

    private async Task UpdateHistory()
    {
        bool hasHistory = !await ComicHistoryItemModel.IsEmptyAsync();
        bool saveBrowsingHistory = AppSettingsModel.Instance.SaveBrowsingHistory;

        IsClearHistoryEnabled = hasHistory;
        SaveBrowsingHistory = saveBrowsingHistory;
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
