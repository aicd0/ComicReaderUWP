// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;

using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Data.Models.Misc;

using static ComicReaderUWP.Views.Pages.Settings.SettingsPageViewModel;

namespace ComicReaderUWP.Views.Pages.Settings;

internal partial class AppearanceSettingsViewModel : INotifyPropertyChanged
{
    private const string TAG = nameof(AppearanceSettingsViewModel);

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

    private List<BackgroundEntry> _backgrounds = [];
    public List<BackgroundEntry> Backgrounds
    {
        get => _backgrounds;
        set
        {
            _backgrounds = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Backgrounds)));
        }
    }

    private int _backgroundIndex = 0;
    public int BackgroundIndex
    {
        get => _backgroundIndex;
        set
        {
            _backgroundIndex = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundIndex)));
        }
    }

    private int _appearanceIndex;
    public int AppearanceIndex
    {
        get => _appearanceIndex;
        set
        {
            _appearanceIndex = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AppearanceIndex)));
        }
    }

    private bool _appearanceChanged;
    public bool AppearanceChanged
    {
        get => _appearanceChanged;
        set
        {
            _appearanceChanged = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AppearanceChanged)));
        }
    }

    public void Initialize(SettingsSharedViewModel shared)
    {
        Shared = shared;
        Shared.UpdateStarted += Update;
    }

    public void SetBackground(int index)
    {
        if (index == _backgroundIndex)
        {
            return;
        }

        if (index < 0 || index >= _backgrounds.Count)
        {
            return;
        }

        AppearanceChanged = true;
        BackgroundEntry selectedBackground = _backgrounds[index];
        _backgroundIndex = index;
        AppSettingsModel.AppBackground = selectedBackground.Value;
    }

    public void SetAppearance(int index)
    {
        if (index == _appearanceIndex)
        {
            return;
        }

        _appearanceIndex = index;
        AppSettingsModel.AppearanceSetting appearance = index switch
        {
            0 => AppSettingsModel.AppearanceSetting.Light,
            1 => AppSettingsModel.AppearanceSetting.Dark,
            2 => AppSettingsModel.AppearanceSetting.UseSystemSetting,
            _ => AppSettingsModel.AppearanceSetting.UseSystemSetting,
        };
        AppearanceChanged = true;
        AppSettingsModel.ExternalModel model = AppSettingsModel.GetModel();
        model.Theme = appearance;
        AppSettingsModel.UpdateModel(model);
    }

    private void Update()
    {
        UpdateAppearance();
        UpdateBackground();

        AppearanceChanged = false;
    }

    private void UpdateBackground()
    {
        AppSettingsModel.ExternalModel model = AppSettingsModel.GetModel();

        AppSettingsModel.AppBackgroundEnum background = AppSettingsModel.AppBackground;
        List<BackgroundEntry> backgrounds = [
            new(StringResourceProvider.Instance.None, AppSettingsModel.AppBackgroundEnum.None),
            new(StringResourceProvider.Instance.BackgroundAcrylic, AppSettingsModel.AppBackgroundEnum.Acrylic),
            new("Mica", AppSettingsModel.AppBackgroundEnum.Mica),
            new("Mica Alt", AppSettingsModel.AppBackgroundEnum.MicaAlt),
        ];
        int backgroundIndex = backgrounds.FindIndex(x => x.Value == background);
        if (backgroundIndex < 0)
        {
            backgroundIndex = 0;
        }

        Backgrounds = backgrounds;
        BackgroundIndex = backgroundIndex;
    }

    private void UpdateAppearance()
    {
        AppSettingsModel.ExternalModel model = AppSettingsModel.GetModel();

        AppSettingsModel.AppearanceSetting appearance = model.Theme;
        if (!Enum.IsDefined(appearance))
        {
            appearance = AppSettingsModel.AppearanceSetting.UseSystemSetting;
        }

        AppearanceIndex = appearance switch
        {
            AppSettingsModel.AppearanceSetting.Light => 0,
            AppSettingsModel.AppearanceSetting.Dark => 1,
            AppSettingsModel.AppearanceSetting.UseSystemSetting => 2,
            _ => 2,
        };
    }
}
