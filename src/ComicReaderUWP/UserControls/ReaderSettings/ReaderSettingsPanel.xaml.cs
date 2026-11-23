// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.BaseUI.PageAbilities;
using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Views.Dialogs.EditReaderSettingPreset;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.UserControls.ReaderSettings;

internal sealed partial class ReaderSettingsPanel : BaseUserControl
{
    #region Constructor

    private const string KEY_SELECTED_TAB = "SelectedReaderSettingsTab";

    public delegate void DataChangedEventHandler(ReaderSettingsModel data);
    public event DataChangedEventHandler? DataChanged;

    public ReaderSettingsPanelViewModel ViewModel { get; } = new();
    public bool ActionInProgress { get; private set; } = false;

    private IMainWindowAbility? _windowAbility;
    private ComicModel? _comic;
    private ReaderSettingsModel _comicSettings = ReaderSettingsModel.FromDefault();
    private readonly ReaderSettingsModel _defaultSettings = ReaderSettingsModel.FromDefault();
    private ReaderSettingsModel? _presetSettings;
    private bool _updatingUI = false;

    public ReaderSettingsPanel()
    {
        InitializeComponent();

        string selectedTab = AppDB.MainRegistry.CreateKey(RegistryNames.SETTINGS).GetValueOrDefault(KEY_SELECTED_TAB, string.Empty);
        SettingsTabSelectorBar.SelectedItem = selectedTab switch
        {
            "ImageProcessing" => SelectorBarItem2,
            _ => SelectorBarItem1,
        };
    }

    #endregion

    #region Public API

    public void Initialize(IMainWindowAbility windowAbility)
    {
        _windowAbility = windowAbility;
    }

    public void SetComic(ComicModel comic)
    {
        if (_comic == comic)
        {
            return;
        }

        _comic = comic;
        LoadSettings();
        UpdateUI();
        DispatchDataChangeEvent();
    }

    #endregion

    #region Lifecycle

    protected override void OnResume()
    {
        base.OnResume();
        ObserveData();
    }

    private void ObserveData()
    {
        ObserveOptions options = new()
        {
            PublishBehavior = LiveDataPublishBehavior.ResumeOnly,
        };

        ViewModel.SettingsChangedLiveData.Observe(this, _ =>
        {
            SaveSettings();
            UpdateUI();
            DispatchDataChangeEvent();
        }, options);
    }

    #endregion

    #region Tab UI

    private void SelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        string selectedTabKey = string.Empty;
        SelectorBarItem selectedItem = sender.SelectedItem;
        if (selectedItem == SelectorBarItem1)
        {
            selectedTabKey = "General";
            GeneralSettingsGrid.Visibility = Visibility.Visible;
            ImageProcessingSettingsGrid.Visibility = Visibility.Collapsed;
        }
        else if (selectedItem == SelectorBarItem2)
        {
            selectedTabKey = "ImageProcessing";
            GeneralSettingsGrid.Visibility = Visibility.Collapsed;
            ImageProcessingSettingsGrid.Visibility = Visibility.Visible;
        }

        AppDB.MainRegistry.CreateKey(RegistryNames.SETTINGS).Set(KEY_SELECTED_TAB, selectedTabKey);
    }

    #endregion

    #region Preset UI

    private async void EditPresetButton_Click(object sender, RoutedEventArgs e)
    {
        IMainWindowAbility? windowAbility = _windowAbility;
        if (_comic is null || windowAbility is null)
        {
            return;
        }

        ActionInProgress = true;
        try
        {
            var dialog = new EditReaderSettingPresetDialog(_comic);
            await dialog.ShowAsync(windowAbility.WindowId);
        }
        finally
        {
            ActionInProgress = false;
        }

        LoadSettings();
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        var newSettings = ReaderSettingsModel.FromDefault();
        newSettings.PresetKey = _comicSettings.PresetKey;
        newSettings.PresetName = _comicSettings.PresetName;
        _comicSettings = newSettings;
        NotifySettingsChanged();
    }

    private MenuFlyout CreatePresetContextMenu(ReaderSettingsModel comicSettings)
    {
        List<Tuple<string, string>> presets = [];
        foreach (KeyValuePair<string, ReaderSettingsModel> kvp in AppSettingsModel.ReaderSettingPresets)
        {
            presets.Add(new Tuple<string, string>(kvp.Value.PresetName, kvp.Key));
        }

        if (presets.Count == 0)
        {
            presets.Add(new Tuple<string, string>(StringResource.Default, ReaderSettingsModel.PRESET_KEY_DEFAULT));
        }

        presets.Sort((a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.Item1, b.Item1));
        presets.Add(new Tuple<string, string>(StringResource.Custom, ReaderSettingsModel.PRESET_KEY_CUSTOM));

        List<BaseMenuFlyoutItemModel> items = [];
        foreach (Tuple<string, string> preset in presets)
        {
            string presetKey = preset.Item2;
            items.Add(new ToggleMenuFlyoutItemModel()
            {
                Text = preset.Item1,
                IsChecked = comicSettings.PresetKey == presetKey,
                Click = () =>
                {
                    var newSettings = ReaderSettingsModel.LoadFromPreset(presetKey);
                    if (newSettings is not null)
                    {
                        _presetSettings = newSettings;
                        _comicSettings = newSettings.Clone();
                        SaveSettings();
                        DispatchDataChangeEvent();
                    }
                    else if (presetKey == ReaderSettingsModel.PRESET_KEY_CUSTOM)
                    {
                        _presetSettings = null;
                        _comicSettings.PresetKey = presetKey;
                        SaveSettings();
                    }
                    else
                    {
                        newSettings = ReaderSettingsModel.FromDefault();
                        _presetSettings = newSettings;
                        _comicSettings = newSettings.Clone();
                        SaveSettings();
                        DispatchDataChangeEvent();
                    }

                    UpdateUI();
                },
            });
        }

        var flyout = new MenuFlyout();
        foreach (BaseMenuFlyoutItemModel item in items)
        {
            flyout.Items.Add(item.CreateMenuFlyoutItem());
        }

        return flyout;
    }

    #endregion

    #region Settings UI

    private void UpdateUI()
    {
        if (_updatingUI)
        {
            return;
        }

        _updatingUI = true;
        try
        {
            UpdateUIInternal();
        }
        finally
        {
            _updatingUI = false;
        }
    }

    private void UpdateUIInternal()
    {
        string GetLabel(string name, bool modified)
        {
            return modified ? name + " *" : name;
        }

        ReaderSettingsModel comicSettings = _comicSettings;
        PageLayoutSettings comicLayoutSettings = comicSettings.IsVertical ? comicSettings.VerticalPageLayout : comicSettings.HorizontalPageLayout;
        ReaderSettingsModel presetSettings = _presetSettings ?? comicSettings;
        PageLayoutSettings presetLayoutSettings = comicSettings.IsVertical ? presetSettings.VerticalPageLayout : presetSettings.HorizontalPageLayout;
        ReaderSettingsModel defaultSettings = _defaultSettings;
        PageLayoutSettings defaultLayoutSettings = comicSettings.IsVertical ? defaultSettings.VerticalPageLayout : defaultSettings.HorizontalPageLayout;

        bool settingsModified = false;

        {
            bool tabModified = false;

            {
                bool modified = comicLayoutSettings.TwoPageMode != presetLayoutSettings.TwoPageMode;
                tabModified = tabModified || modified;
                TwoPageModeToggleSwitch.IsOn = comicLayoutSettings.TwoPageMode;
                ViewModel.TwoPageModeLabel = GetLabel(StringResource.TwoPageMode, modified);
            }

            {
                bool modified = comicLayoutSettings.CoverPageCount != presetLayoutSettings.CoverPageCount;
                tabModified = tabModified || modified;
                CoverPageCountSlider.Value = Math.Clamp(comicLayoutSettings.CoverPageCount, 0, 2);
                CoverPageCountSlider.RestoreValue = presetLayoutSettings.CoverPageCount;
                CoverPageCountSlider.ResetValue = defaultLayoutSettings.CoverPageCount;
                CoverPageCountSlider.Visibility = comicLayoutSettings.TwoPageMode ? Visibility.Visible : Visibility.Collapsed;
            }

            {
                bool modified = comicLayoutSettings.SwapLeftAndRightPages != presetLayoutSettings.SwapLeftAndRightPages;
                tabModified = tabModified || modified;
                SwapLeftAndRightPagesToggleSwitch.IsOn = comicLayoutSettings.SwapLeftAndRightPages;
                SwapLeftAndRightPagesGrid.Visibility = comicLayoutSettings.TwoPageMode ? Visibility.Visible : Visibility.Collapsed;
                ViewModel.SwapLeftAndRightPagesLabel = GetLabel(StringResource.SwapLeftAndRightPages, modified);
            }

            {
                bool modified = comicLayoutSettings.SpreadDetection != presetLayoutSettings.SpreadDetection;
                tabModified = tabModified || modified;
                SpreadDetectionToggleSwitch.IsOn = comicLayoutSettings.SpreadDetection;
                ViewModel.SpreadDetectionLabel = GetLabel(StringResource.SpreadDetection, modified);
            }

            {
                bool modified = comicSettings.IsVertical != presetSettings.IsVertical;
                tabModified = tabModified || modified;
                AbbOrientation.Label = GetLabel(
                    comicSettings.IsVertical ? StringResource.Vertical : StringResource.Horizontal,
                    modified);
                AbbOrientation.Content = new FontIcon() { Glyph = comicSettings.IsVertical ? "\uE7C3" : "\uEF6B" };
            }

            {
                bool modified = comicSettings.IsLeftToRight != presetSettings.IsLeftToRight;
                tabModified = tabModified || modified;
                AbbFlowDirection.Label = GetLabel(
                    comicSettings.IsLeftToRight ? StringResource.LeftToRight : StringResource.RightToLeft,
                    modified);
                AbbFlowDirection.Content = new FontIcon() { Glyph = comicSettings.IsLeftToRight ? "\uEBE7" : "\uEC52" };
            }

            {
                bool modified = comicSettings.IsContinuous != presetSettings.IsContinuous;
                tabModified = tabModified || modified;
                AbbContinuous.Label = GetLabel(
                    comicSettings.IsContinuous ? StringResource.Continuous : StringResource.Separate,
                    modified);
                AbbContinuous.Content = new FontIcon() { Glyph = comicSettings.IsContinuous ? "\uE785" : "\uE72E" };
            }

            {
                bool modified = comicSettings.PageSpacing != presetSettings.PageSpacing;
                tabModified = tabModified || modified;
                PageSpacingSlider.Value = Math.Clamp(comicSettings.PageSpacing, 0, 200);
                PageSpacingSlider.RestoreValue = presetSettings.PageSpacing;
                PageSpacingSlider.ResetValue = defaultSettings.PageSpacing;
            }

            {
                bool modified = comicSettings.AutoScrollSpeed != presetSettings.AutoScrollSpeed;
                tabModified = tabModified || modified;
                AutoScrollingSlider.Value = Math.Clamp(comicSettings.AutoScrollSpeed, 0, 100);
                AutoScrollingSlider.RestoreValue = presetSettings.AutoScrollSpeed;
                AutoScrollingSlider.ResetValue = defaultSettings.AutoScrollSpeed;
            }

            {
                bool modified = comicSettings.OriginalSize != presetSettings.OriginalSize;
                tabModified = tabModified || modified;
                OriginalSizeToggleSwitch.IsOn = comicSettings.OriginalSize;
                ViewModel.OriginalSizeLabel = GetLabel(StringResource.MaintainRelativeSize, modified);
            }

            settingsModified = settingsModified || tabModified;
            ViewModel.GeneralTabTitle = GetLabel(StringResource.General, tabModified);
        }

        {
            bool tabModified = false;

            {
                bool modified = comicSettings.ImageRotation != presetSettings.ImageRotation;
                tabModified = tabModified || modified;
                UpdateImageRotation(comicSettings);
                ViewModel.RotationLabel = GetLabel(StringResource.Rotation, modified);
            }

            {
                bool modified = comicSettings.ImageFlip != presetSettings.ImageFlip;
                tabModified = tabModified || modified;
                FlipImageToggleSwitch.IsOn = comicSettings.ImageFlip;
                ViewModel.FlipImageLabel = GetLabel(StringResource.FlipImage, modified);
            }

            {
                bool modified = comicSettings.AntiAliasingFilterPercentage != presetSettings.AntiAliasingFilterPercentage;
                tabModified = tabModified || modified;
                AntiAliasingFilterSlider.Value = Math.Clamp(comicSettings.AntiAliasingFilterPercentage, 0, 200);
                AntiAliasingFilterSlider.RestoreValue = presetSettings.AntiAliasingFilterPercentage;
                AntiAliasingFilterSlider.ResetValue = defaultSettings.AntiAliasingFilterPercentage;
            }

            {
                bool modified = comicSettings.BrightnessPercentage != presetSettings.BrightnessPercentage;
                tabModified = tabModified || modified;
                BrightnessSlider.Value = Math.Clamp(comicSettings.BrightnessPercentage, 0, 100);
                BrightnessSlider.RestoreValue = presetSettings.BrightnessPercentage;
                BrightnessSlider.ResetValue = defaultSettings.BrightnessPercentage;
            }

            {
                bool modified = comicSettings.ContrastPercentage != presetSettings.ContrastPercentage;
                tabModified = tabModified || modified;
                ContrastSlider.Value = Math.Clamp(comicSettings.ContrastPercentage, 0, 100);
                ContrastSlider.RestoreValue = presetSettings.ContrastPercentage;
                ContrastSlider.ResetValue = defaultSettings.ContrastPercentage;
            }

            {
                bool modified = comicSettings.SaturationPercentage != presetSettings.SaturationPercentage;
                tabModified = tabModified || modified;
                SaturationSlider.Value = Math.Clamp(comicSettings.SaturationPercentage, 0, 100);
                SaturationSlider.RestoreValue = presetSettings.SaturationPercentage;
                SaturationSlider.ResetValue = defaultSettings.SaturationPercentage;
            }

            {
                bool modified = comicSettings.ImageInvert != presetSettings.ImageInvert;
                tabModified = tabModified || modified;
                InvertImageToggleSwitch.IsOn = comicSettings.ImageInvert;
                ViewModel.InvertImageLabel = GetLabel(StringResource.InvertImage, modified);
            }

            settingsModified = settingsModified || tabModified;
            ViewModel.ImageProcessingTabTitle = GetLabel(StringResource.Image, tabModified);
        }

        PresetDropDownButton.Flyout = CreatePresetContextMenu(comicSettings);
        PresetDropDownButton.Content = GetLabel(
            comicSettings.PresetKey == ReaderSettingsModel.PRESET_KEY_CUSTOM ?
                StringResource.Custom : comicSettings.PresetName,
            settingsModified);
    }

    private void TwoPageModeToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        PageLayoutSettings pageLayoutSettings = _comicSettings.IsVertical ? _comicSettings.VerticalPageLayout : _comicSettings.HorizontalPageLayout;
        pageLayoutSettings.TwoPageMode = ((ToggleSwitch)sender).IsOn;
        NotifySettingsChanged();
    }

    private void CoverPageCountSlider_ValueChanged(object? sender, EventArgs e)
    {
        if (_updatingUI)
        {
            return;
        }

        PageLayoutSettings pageLayoutSettings = _comicSettings.IsVertical ? _comicSettings.VerticalPageLayout : _comicSettings.HorizontalPageLayout;
        int value = Math.Clamp((int)Math.Round(CoverPageCountSlider.Value), 0, 2);
        pageLayoutSettings.CoverPageCount = value;
        NotifySettingsChanged();
    }

    private void SwapLeftAndRightPagesToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        PageLayoutSettings pageLayoutSettings = _comicSettings.IsVertical ? _comicSettings.VerticalPageLayout : _comicSettings.HorizontalPageLayout;
        pageLayoutSettings.SwapLeftAndRightPages = ((ToggleSwitch)sender).IsOn;
        NotifySettingsChanged();
    }

    private void SpreadDetectionToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        PageLayoutSettings pageLayoutSettings = _comicSettings.IsVertical ? _comicSettings.VerticalPageLayout : _comicSettings.HorizontalPageLayout;
        pageLayoutSettings.SpreadDetection = ((ToggleSwitch)sender).IsOn;
        NotifySettingsChanged();
    }

    private void AbbOrientation_Click(object sender, RoutedEventArgs e)
    {
        _comicSettings.IsVertical = !_comicSettings.IsVertical;
        NotifySettingsChanged();
    }

    private void AbbFlowDirection_Click(object sender, RoutedEventArgs e)
    {
        _comicSettings.IsLeftToRight = !_comicSettings.IsLeftToRight;
        NotifySettingsChanged();
    }

    private void AbbContinuous_Click(object sender, RoutedEventArgs e)
    {
        _comicSettings.IsContinuous = !_comicSettings.IsContinuous;
        NotifySettingsChanged();
    }

    private void PageSpacingSlider_ValueChanged(object? sender, EventArgs e)
    {
        if (_updatingUI)
        {
            return;
        }

        int value = Math.Clamp((int)Math.Round(PageSpacingSlider.Value), 0, 200);
        _comicSettings.PageSpacing = value;
        NotifySettingsChanged();
    }

    private void AutoScrollingSlider_ValueChanged(object? sender, EventArgs e)
    {
        if (_updatingUI)
        {
            return;
        }

        int value = Math.Clamp((int)Math.Round(AutoScrollingSlider.Value), 0, 100);
        _comicSettings.AutoScrollSpeed = value;
        NotifySettingsChanged();
    }

    private void OriginalSizeToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        _comicSettings.OriginalSize = OriginalSizeToggleSwitch.IsOn;
        NotifySettingsChanged();
    }

    private void UpdateImageRotation(ReaderSettingsModel comicSettings)
    {
        List<Tuple<string, ImageRotationEnum>> rotations =
        [
            new(StringResource.None, ImageRotationEnum.None),
            new("90º", ImageRotationEnum.Rotate90),
            new("180º", ImageRotationEnum.Rotate180),
            new("270º", ImageRotationEnum.Rotate270),
        ];

        List<BaseMenuFlyoutItemModel> items = [];
        foreach (Tuple<string, ImageRotationEnum> rotation in rotations)
        {
            ImageRotationEnum rotationValue = rotation.Item2;
            items.Add(new ToggleMenuFlyoutItemModel()
            {
                Text = rotation.Item1,
                IsChecked = comicSettings.ImageRotation == rotationValue,
                Click = () =>
                {
                    if (comicSettings.ImageRotation != rotationValue)
                    {
                        comicSettings.ImageRotation = rotationValue;
                        SaveSettings();
                        DispatchDataChangeEvent();
                    }

                    UpdateUI();
                },
            });
        }

        var flyout = new MenuFlyout();
        foreach (BaseMenuFlyoutItemModel item in items)
        {
            flyout.Items.Add(item.CreateMenuFlyoutItem());
        }

        RotationDropDownButton.Flyout = flyout;
        RotationDropDownButton.Content = rotations.Find(r => r.Item2 == comicSettings.ImageRotation)?.Item1 ?? StringResource.None;
    }

    private void FlipImageToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        _comicSettings.ImageFlip = FlipImageToggleSwitch.IsOn;
        NotifySettingsChanged();
    }

    private void AntiAliasingFilterSlider_ValueChanged(object? sender, EventArgs e)
    {
        if (_updatingUI)
        {
            return;
        }

        int value = Math.Clamp((int)Math.Round(AntiAliasingFilterSlider.Value), 0, 200);
        _comicSettings.AntiAliasingFilterPercentage = value;
        NotifySettingsChanged();
    }

    private void BrightnessSlider_ValueChanged(object? sender, EventArgs e)
    {
        if (_updatingUI)
        {
            return;
        }

        int value = Math.Clamp((int)Math.Round(BrightnessSlider.Value), 0, 100);
        _comicSettings.BrightnessPercentage = value;
        NotifySettingsChanged();
    }

    private void ContrastSlider_ValueChanged(object? sender, EventArgs e)
    {
        if (_updatingUI)
        {
            return;
        }

        int value = Math.Clamp((int)Math.Round(ContrastSlider.Value), 0, 100);
        _comicSettings.ContrastPercentage = value;
        NotifySettingsChanged();
    }

    private void SaturationSlider_ValueChanged(object? sender, EventArgs e)
    {
        if (_updatingUI)
        {
            return;
        }

        int value = Math.Clamp((int)Math.Round(SaturationSlider.Value), 0, 100);
        _comicSettings.SaturationPercentage = value;
        NotifySettingsChanged();
    }

    private void InvertImageToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        _comicSettings.ImageInvert = InvertImageToggleSwitch.IsOn;
        NotifySettingsChanged();
    }

    #endregion

    #region Settings

    private void LoadSettings()
    {
        if (_comic is null)
        {
            return;
        }

        _comicSettings = ReaderSettingsModel.LoadFromComic(_comic);
        _presetSettings = ReaderSettingsModel.LoadFromPreset(_comicSettings.PresetKey);
    }

    private void SaveSettings()
    {
        if (_updatingUI || _comic is null)
        {
            return;
        }

        _comicSettings.SaveToComic(_comic);
        _presetSettings = ReaderSettingsModel.LoadFromPreset(_comicSettings.PresetKey);
    }

    private void NotifySettingsChanged()
    {
        ViewModel.SettingsChangedLiveData.Emit(true);
    }

    private void DispatchDataChangeEvent()
    {
        if (_updatingUI)
        {
            return;
        }

        DataChanged?.Invoke(_comicSettings);
    }

    #endregion
}
