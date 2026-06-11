// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Core.Common.Lifecycle;
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

internal sealed partial class ReaderSettingsPanel : BaseUserControl
{
    private const string KEY_SELECTED_TAB = "SelectedReaderSettingsTab";

    public delegate void DataChangedEventHandler(ReaderSettingsModel data);
    public event DataChangedEventHandler? DataChanged;

    public ReaderSettingsPanelViewModel ViewModel { get; } = new();
    public bool ActionInProgress { get; private set; } = false;

    private int _windowId = -1;
    private ComicModel? _comic;
    private ReaderSettingsModel _comicSettings = ReaderSettingsModel.FromDefault();
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
        _comicSettings = ReaderSettingsModel.LoadFromComic(comic);
        _presetSettings = ReaderSettingsModel.LoadFromPreset(_comicSettings.PresetKey);
        UpdateUI();
        DispatchDataChangeEvent();
    }

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
        PageLayoutSettings pageLayoutSettings = _comicSettings.IsVertical ? _comicSettings.VerticalPageLayout : _comicSettings.HorizontalPageLayout;
        pageLayoutSettings.TwoPageMode = ((ToggleSwitch)sender).IsOn;
        NotifySettingsChanged();
    }

    private void EnableCoverToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        PageLayoutSettings pageLayoutSettings = _comicSettings.IsVertical ? _comicSettings.VerticalPageLayout : _comicSettings.HorizontalPageLayout;
        pageLayoutSettings.EnableCover = ((ToggleSwitch)sender).IsOn;
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

    private void PageSpacingSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        _comicSettings.PageSpacing = Math.Clamp((int)e.NewValue, 0, 200);
        NotifySettingsChanged();
    }

    private void AutoScrollingSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        _comicSettings.AutoScrollSpeed = Math.Clamp((int)e.NewValue, 0, 100);
        NotifySettingsChanged();
    }

    private void OriginalSizeToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        _comicSettings.OriginalSize = OriginalSizeToggleSwitch.IsOn;
        NotifySettingsChanged();
    }

    private void FlipImageToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        _comicSettings.ImageFlip = FlipImageToggleSwitch.IsOn;
        NotifySettingsChanged();
    }

    private void InvertImageToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        _comicSettings.ImageInvert = InvertImageToggleSwitch.IsOn;
        NotifySettingsChanged();
    }

    private void AntiAliasingFilterToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        _comicSettings.AntiAliasingFilter = AntiAliasingFilterToggleSwitch.IsOn;
        NotifySettingsChanged();
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

        _comicSettings = ReaderSettingsModel.LoadFromComic(_comic);
        _presetSettings = ReaderSettingsModel.LoadFromPreset(_comicSettings.PresetKey);
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

    private void NotifySettingsChanged()
    {
        ViewModel.SettingsChangedLiveData.Emit(true);
    }

    private void SaveSettings()
    {
        if (_updatingUI || _comic is null)
        {
            return;
        }

        if (_comic is not null && !_comic.IsExternal)
        {
            _comicSettings.SaveToComic(_comic);
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
        string GetLabel(string name, bool modified)
        {
            return modified ? name + " *" : name;
        }

        string GetLabelCompat(string name, bool modified)
        {
            return modified ? name + "*" : name;
        }

        ReaderSettingsModel comicSettings = _comicSettings;
        PageLayoutSettings comicLayoutSettings = comicSettings.IsVertical ? comicSettings.VerticalPageLayout : comicSettings.HorizontalPageLayout;
        ReaderSettingsModel presetSettings = _presetSettings ?? comicSettings;
        PageLayoutSettings presetLayoutSettings = comicSettings.IsVertical ? presetSettings.VerticalPageLayout : presetSettings.HorizontalPageLayout;

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
                bool modified = comicLayoutSettings.EnableCover != presetLayoutSettings.EnableCover;
                tabModified = tabModified || modified;
                EnableCoverToggleSwitch.IsOn = comicLayoutSettings.EnableCover;
                EnableCoverToggleSwitch.IsEnabled = comicLayoutSettings.TwoPageMode;
                ViewModel.EnableCoverLabel = GetLabel(StringResource.EnableCover, modified);
            }

            {
                bool modified = comicLayoutSettings.SwapLeftAndRightPages != presetLayoutSettings.SwapLeftAndRightPages;
                tabModified = tabModified || modified;
                SwapLeftAndRightPagesToggleSwitch.IsOn = comicLayoutSettings.SwapLeftAndRightPages;
                SwapLeftAndRightPagesToggleSwitch.IsEnabled = comicLayoutSettings.TwoPageMode;
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
                AbbOrientation.Label = GetLabelCompat(
                    comicSettings.IsVertical ? StringResource.Vertical : StringResource.Horizontal,
                    modified);
                AbbOrientation.Content = new FontIcon() { Glyph = comicSettings.IsVertical ? "\uE7C3" : "\uEF6B" };
            }

            {
                bool modified = comicSettings.IsLeftToRight != presetSettings.IsLeftToRight;
                tabModified = tabModified || modified;
                AbbFlowDirection.Label = GetLabelCompat(
                    comicSettings.IsLeftToRight ? StringResource.LeftToRight : StringResource.RightToLeft,
                    modified);
                AbbFlowDirection.Content = new FontIcon() { Glyph = comicSettings.IsLeftToRight ? "\uEBE7" : "\uEC52" };
            }

            {
                bool modified = comicSettings.IsContinuous != presetSettings.IsContinuous;
                tabModified = tabModified || modified;
                AbbContinuous.Label = GetLabelCompat(
                    comicSettings.IsContinuous ? StringResource.Continuous : StringResource.Separate,
                    modified);
                AbbContinuous.Content = new FontIcon() { Glyph = comicSettings.IsContinuous ? "\uE785" : "\uE72E" };
            }

            {
                bool modified = comicSettings.PageSpacing != presetSettings.PageSpacing;
                tabModified = tabModified || modified;
                PageSpacingSlider.Value = Math.Clamp(comicSettings.PageSpacing, 0, 200);
                ViewModel.PageSpacingLabel = GetLabel(StringResource.PageSpacing, modified);
            }

            {
                bool modified = comicSettings.AutoScrollSpeed != presetSettings.AutoScrollSpeed;
                tabModified = tabModified || modified;
                AutoScrollingSlider.Value = Math.Clamp(comicSettings.AutoScrollSpeed, 0, 100);
                ViewModel.AutoScrollingLabel = GetLabel(StringResource.AutoScrolling, modified);
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
                bool modified = comicSettings.ImageInvert != presetSettings.ImageInvert;
                tabModified = tabModified || modified;
                InvertImageToggleSwitch.IsOn = comicSettings.ImageInvert;
                ViewModel.InvertImageLabel = GetLabel(StringResource.InvertImage, modified);
            }

            {
                bool modified = comicSettings.AntiAliasingFilter != presetSettings.AntiAliasingFilter;
                tabModified = tabModified || modified;
                AntiAliasingFilterToggleSwitch.IsOn = comicSettings.AntiAliasingFilter;
                ViewModel.AntiAliasingFilterLabel = GetLabel(StringResource.AntiAliasingFilter, modified);
            }

            settingsModified = settingsModified || tabModified;
            ViewModel.ImageProcessingTabTitle = GetLabel(StringResource.ImageProcessing, tabModified);
        }

        PresetDropDownButton.Flyout = CreatePresetContextMenu(comicSettings);
        PresetDropDownButton.Content = GetLabel(
            comicSettings.PresetKey == ReaderSettingsModel.PRESET_KEY_CUSTOM ?
                StringResource.Custom : comicSettings.PresetName,
            settingsModified);
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

    private MenuFlyout CreatePresetContextMenu(ReaderSettingsModel comicSettings)
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

    private void DispatchDataChangeEvent()
    {
        if (_updatingUI)
        {
            return;
        }

        DataChanged?.Invoke(_comicSettings);
    }
}
