// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
using ComicReaderUWP.Views.Dialogs.EditReaderSettingPreset;
using ComicReaderUWP.Views.Pages.Main;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace ComicReaderUWP.UserControls.ReaderSettings;

internal sealed partial class ReaderSettingPanel : BaseUserControl
{
    private const string KEY_SELECTED_TAB = "SelectedReaderSettingsTab";

    public delegate void DataChangedEventHandler(ReaderSettingsModel data);
    public event DataChangedEventHandler? DataChanged;

    public bool ActionInProgress { get; private set; } = false;

    private int _windowId = -1;
    private ComicModel? _comic;
    private ReaderSettingsModel _model = new();
    private bool _updatingUI = false;

    public ReaderSettingPanel()
    {
        InitializeComponent();

        string selectedTab = AppDB.MainRegistry.CreateKey(RegistryNames.SETTINGS).GetValueOrDefault(KEY_SELECTED_TAB, string.Empty);
        SettingsTabSelectorBar.SelectedItem = selectedTab switch
        {
            "ImageProcessing" => SelectorBarItem2,
            _ => SelectorBarItem1,
        };
    }

    public void SetWindowId(int windowId)
    {
        _windowId = windowId;
    }

    public void SetComic(ComicModel comic)
    {
        if (_comic == comic)
        {
            return;
        }

        _comic = comic;
        _model = ReaderSettingsModel.LoadFromComic(comic);
        UpdateUI();
        DispatchDataChangeEvent();
    }

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

    private void TwoPageModeToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        PageLayoutSettings pageLayoutSettings = _model.IsVertical ? _model.VerticalPageLayout : _model.HorizontalPageLayout;
        pageLayoutSettings.TwoPageMode = ((ToggleSwitch)sender).IsOn;
        SaveSettings();
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private void EnableCoverToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        PageLayoutSettings pageLayoutSettings = _model.IsVertical ? _model.VerticalPageLayout : _model.HorizontalPageLayout;
        pageLayoutSettings.EnableCover = ((ToggleSwitch)sender).IsOn;
        SaveSettings();
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private void SwapLeftAndRightPagesToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        PageLayoutSettings pageLayoutSettings = _model.IsVertical ? _model.VerticalPageLayout : _model.HorizontalPageLayout;
        pageLayoutSettings.SwapLeftAndRightPages = ((ToggleSwitch)sender).IsOn;
        SaveSettings();
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private void SpreadDetectionToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        PageLayoutSettings pageLayoutSettings = _model.IsVertical ? _model.VerticalPageLayout : _model.HorizontalPageLayout;
        pageLayoutSettings.SpreadDetection = ((ToggleSwitch)sender).IsOn;
        SaveSettings();
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private void AbbVertical_Click(object sender, RoutedEventArgs e)
    {
        _model.IsVertical = false;
        SaveSettings();
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private void AbbHorizontal_Click(object sender, RoutedEventArgs e)
    {
        _model.IsVertical = true;
        SaveSettings();
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private void AbbLeftToRight_Click(object sender, RoutedEventArgs e)
    {
        _model.IsLeftToRight = false;
        SaveSettings();
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private void AbbRightToLeft_Click(object sender, RoutedEventArgs e)
    {
        _model.IsLeftToRight = true;
        SaveSettings();
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private void AbbSeperate_Click(object sender, RoutedEventArgs e)
    {
        _model.IsContinuous = true;
        SaveSettings();
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private void AbbContinuous_Click(object sender, RoutedEventArgs e)
    {
        _model.IsContinuous = false;
        SaveSettings();
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private void PageGapSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        _model.PageGap = Math.Clamp((int)e.NewValue, 0, 200);
        SaveSettings();
        DispatchDataChangeEvent();
    }

    private void AutoScrollingSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        _model.AutoScrollSpeed = Math.Clamp((int)e.NewValue, 0, 100);
        SaveSettings();
        DispatchDataChangeEvent();
    }

    private void OriginalSizeToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        _model.OriginalSize = OriginalSizeToggleSwitch.IsOn;
        SaveSettings();
        DispatchDataChangeEvent();
    }

    private void FlipImageToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        _model.ImageFlip = FlipImageToggleSwitch.IsOn;
        SaveSettings();
        DispatchDataChangeEvent();
    }

    private void InvertImageToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        _model.ImageInvert = InvertImageToggleSwitch.IsOn;
        SaveSettings();
        DispatchDataChangeEvent();
    }

    private void AntiAliasingFilterToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        _model.AntiAliasingFilter = AntiAliasingFilterToggleSwitch.IsOn;
        SaveSettings();
        DispatchDataChangeEvent();
    }

    private async void EditPresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_comic is null || _windowId < 0)
        {
            return;
        }

        ActionInProgress = true;
        try
        {
            var dialog = new EditReaderSettingPresetDialog(_comic);
            await dialog.ShowAsync(_windowId);
        }
        finally
        {
            ActionInProgress = false;
        }

        _model = ReaderSettingsModel.LoadFromComic(_comic);
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _model = new ReaderSettingsModel
        {
            PresetKey = _model.PresetKey,
            PresetName = _model.PresetName,
        };

        SaveSettings();
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private void SaveSettings()
    {
        if (_updatingUI || _comic is null)
        {
            return;
        }

        if (_model.PresetKey == ReaderSettingsModel.PRESET_KEY_CUSTOM)
        {
            if (_comic is not null && !_comic.IsExternal)
            {
                _model.SaveToComic(_comic);
            }
        }
        else
        {
            Dictionary<string, ReaderSettingsModel> presets = AppSettingsModel.Instance.ReaderSettingPresets;
            presets[_model.PresetKey] = _model;
            AppSettingsModel.Instance.ReaderSettingPresets = presets;
        }
    }

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
        PageLayoutSettings pageLayoutSettings = _model.IsVertical ? _model.VerticalPageLayout : _model.HorizontalPageLayout;
        TwoPageModeToggleSwitch.IsOn = pageLayoutSettings.TwoPageMode;
        EnableCoverToggleSwitch.IsOn = pageLayoutSettings.EnableCover;
        SwapLeftAndRightPagesToggleSwitch.IsOn = pageLayoutSettings.SwapLeftAndRightPages;
        SpreadDetectionToggleSwitch.IsOn = pageLayoutSettings.SpreadDetection;
        EnableCoverToggleSwitch.IsEnabled = pageLayoutSettings.TwoPageMode;
        SwapLeftAndRightPagesToggleSwitch.IsEnabled = pageLayoutSettings.TwoPageMode;
        SpreadDetectionToggleSwitch.IsEnabled = pageLayoutSettings.TwoPageMode;

        AbbVertical.Visibility = _model.IsVertical ? Visibility.Visible : Visibility.Collapsed;
        AbbHorizontal.Visibility = _model.IsVertical ? Visibility.Collapsed : Visibility.Visible;
        AbbLeftToRight.Visibility = (!_model.IsVertical || _model.VerticalPageLayout.TwoPageMode) && _model.IsLeftToRight ? Visibility.Visible : Visibility.Collapsed;
        AbbRightToLeft.Visibility = (!_model.IsVertical || _model.VerticalPageLayout.TwoPageMode) && !_model.IsLeftToRight ? Visibility.Visible : Visibility.Collapsed;
        AbbContinuous.Visibility = _model.IsContinuous ? Visibility.Visible : Visibility.Collapsed;
        AbbSeperate.Visibility = _model.IsContinuous ? Visibility.Collapsed : Visibility.Visible;

        OriginalSizeToggleSwitch.IsOn = _model.OriginalSize;
        PageGapSlider.Value = Math.Clamp(_model.PageGap, 0, 200);
        AutoScrollingSlider.Value = Math.Clamp(_model.AutoScrollSpeed, 0, 100);
        FlipImageToggleSwitch.IsOn = _model.ImageFlip;
        InvertImageToggleSwitch.IsOn = _model.ImageInvert;
        AntiAliasingFilterToggleSwitch.IsOn = _model.AntiAliasingFilter;

        UpdateImageRotation();

        PresetDropDownButton.Flyout = CreatePresetContextMenu();
        PresetDropDownButton.Content = _model.PresetKey == ReaderSettingsModel.PRESET_KEY_CUSTOM ? StringResource.Custom : _model.PresetName;
    }

    private void UpdateImageRotation()
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
                IsChecked = _model.ImageRotation == rotationValue,
                Click = () =>
                {
                    if (_model.ImageRotation != rotationValue)
                    {
                        _model.ImageRotation = rotationValue;
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
        RotationDropDownButton.Content = rotations.Find(r => r.Item2 == _model.ImageRotation)?.Item1 ?? StringResource.None;
    }

    private MenuFlyout CreatePresetContextMenu()
    {
        List<Tuple<string, string>> presets = [];
        foreach (KeyValuePair<string, ReaderSettingsModel> kvp in AppSettingsModel.Instance.ReaderSettingPresets)
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
                IsChecked = _model.PresetKey == presetKey,
                Click = () =>
                {
                    if (presetKey != _model.PresetKey && _comic is not null)
                    {
                        _model.PresetKey = presetKey;
                        _model.SaveToComic(_comic);
                        _model = ReaderSettingsModel.LoadFromComic(_comic);
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

    private void DispatchDataChangeEvent()
    {
        if (_updatingUI)
        {
            return;
        }

        DataChanged?.Invoke(_model);
    }
}
