// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReader.Common.BaseUI;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.MenuFlyoutHelpers;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.Views.Dialogs.EditReaderSettingPreset;
using ComicReader.Views.Pages.Navigation;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace ComicReader.Views.Pages.Reader;

internal sealed partial class ReaderSettingPanel : BaseUserControl
{
    public delegate void DataChangedEventHandler(ReaderSettingDataModel data);
    public event DataChangedEventHandler? DataChanged;

    public bool ActionInProgress { get; private set; } = false;

    private int _windowId = -1;
    private ComicModel? _comic;
    private ReaderSettingDataModel _model = new();
    private bool _updatingUI = false;

    public ReaderSettingPanel()
    {
        InitializeComponent();
    }

    public void SetWindowId(int windowId)
    {
        _windowId = windowId;
    }

    public void SetComic(ComicModel comic)
    {
        _comic = comic;
        _model = ReaderSettingDataModel.FromComic(comic);
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private void LvPageArrangement_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        PageArrangementEnum pageArrangement = IndexToPageArrangement(LvPageArrangement.SelectedIndex);
        if (_model.IsVertical)
        {
            _model.VerticalPageArrangement = pageArrangement;
        }
        else
        {
            _model.HorizontalPageArrangement = pageArrangement;
        }

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
            _ = await dialog.ShowAsync(_windowId);
        }
        finally
        {
            ActionInProgress = false;
        }

        _model = ReaderSettingDataModel.FromComic(_comic);
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _model = new ReaderSettingDataModel
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

        if (_model.PresetKey == ReaderSettingDataModel.PRESET_KEY_CUSTOM)
        {
            if (_comic is not null && !_comic.IsExternal)
            {
                _model.ToComic(_comic);
            }
        }
        else
        {
            AppSettingsModel.ExternalModel settingsModel = AppSettingsModel.Instance.GetModel();
            settingsModel.ReaderSettingPresets[_model.PresetKey] = _model.ToSettingModel();
            AppSettingsModel.Instance.UpdateModel(settingsModel);
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
        PageArrangementEnum pageArrangement = _model.IsVertical ? _model.VerticalPageArrangement : _model.HorizontalPageArrangement;
        LvPageArrangement.SelectedIndex = PageArrangementToIndex(pageArrangement);
        PdsDemoSingle1.IsHighlight = pageArrangement == PageArrangementEnum.Single;
        PdsDemoSingle2.IsHighlight = pageArrangement == PageArrangementEnum.Single;
        PdsDemoSingle3.IsHighlight = pageArrangement == PageArrangementEnum.Single;
        PdsDemoSingle4.IsHighlight = pageArrangement == PageArrangementEnum.Single;
        PdsDemoSingle5.IsHighlight = pageArrangement == PageArrangementEnum.Single;
        PdsDemoDual1.IsHighlight = pageArrangement == PageArrangementEnum.DualCover;
        PdsDemoDual2.IsHighlight = pageArrangement == PageArrangementEnum.DualCover;
        PdsDemoDual3.IsHighlight = pageArrangement == PageArrangementEnum.DualCover;
        PdsDemoDualCoverMirror1.IsHighlight = pageArrangement == PageArrangementEnum.DualCoverMirror;
        PdsDemoDualCoverMirror2.IsHighlight = pageArrangement == PageArrangementEnum.DualCoverMirror;
        PdsDemoDualCoverMirror3.IsHighlight = pageArrangement == PageArrangementEnum.DualCoverMirror;
        PdsDemoDualNoCover1.IsHighlight = pageArrangement == PageArrangementEnum.DualNoCover;
        PdsDemoDualNoCover2.IsHighlight = pageArrangement == PageArrangementEnum.DualNoCover;
        PdsDemoDualNoCover3.IsHighlight = pageArrangement == PageArrangementEnum.DualNoCover;
        PdsDemoDualNoCoverMirror1.IsHighlight = pageArrangement == PageArrangementEnum.DualNoCoverMirror;
        PdsDemoDualNoCoverMirror2.IsHighlight = pageArrangement == PageArrangementEnum.DualNoCoverMirror;
        PdsDemoDualNoCoverMirror3.IsHighlight = pageArrangement == PageArrangementEnum.DualNoCoverMirror;

        FlowDirection flowDirection = _model.IsLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        FlowDirection demoPageFlowDirection = _model.IsVertical ? FlowDirection.LeftToRight : flowDirection;
        SpDemoSingle.FlowDirection = demoPageFlowDirection;
        SpDemoDualCover.FlowDirection = demoPageFlowDirection;
        SpDemoDualCoverMirror.FlowDirection = demoPageFlowDirection;
        SpDemoDualNoCover.FlowDirection = demoPageFlowDirection;
        SpDemoDualNoCoverMirror.FlowDirection = demoPageFlowDirection;

        AbbVertical.Visibility = _model.IsVertical ? Visibility.Visible : Visibility.Collapsed;
        AbbHorizontal.Visibility = _model.IsVertical ? Visibility.Collapsed : Visibility.Visible;
        AbbLeftToRight.Visibility = !_model.IsVertical && _model.IsLeftToRight ? Visibility.Visible : Visibility.Collapsed;
        AbbRightToLeft.Visibility = !_model.IsVertical && !_model.IsLeftToRight ? Visibility.Visible : Visibility.Collapsed;
        AbbContinuous.Visibility = _model.IsContinuous ? Visibility.Visible : Visibility.Collapsed;
        AbbSeperate.Visibility = _model.IsContinuous ? Visibility.Collapsed : Visibility.Visible;

        OriginalSizeToggleSwitch.IsOn = _model.OriginalSize;
        PageGapSlider.Value = Math.Clamp(_model.PageGap, 0, 200);
        AutoScrollingSlider.Value = Math.Clamp(_model.AutoScrollSpeed, 0, 100);

        PresetDropDownButton.Flyout = CreatePresetContextMenu();
        PresetDropDownButton.Content = _model.PresetKey == ReaderSettingDataModel.PRESET_KEY_CUSTOM ? StringResource.Custom : _model.PresetName;
    }

    private MenuFlyout CreatePresetContextMenu()
    {
        AppSettingsModel.ExternalModel settingModel = AppSettingsModel.Instance.GetModel();
        List<Tuple<string, string>> presets = [];
        foreach (KeyValuePair<string, AppSettingsModel.ReaderSettingModel> kvp in settingModel.ReaderSettingPresets)
        {
            presets.Add(new Tuple<string, string>(kvp.Value.PresetName, kvp.Key));
        }

        if (presets.Count == 0)
        {
            presets.Add(new Tuple<string, string>(StringResource.Default, ReaderSettingDataModel.PRESET_KEY_DEFAULT));
        }

        presets.Sort((a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.Item1, b.Item1));
        presets.Add(new Tuple<string, string>(StringResource.Custom, ReaderSettingDataModel.PRESET_KEY_CUSTOM));

        List<BaseMenuFlyoutItemViewModel> items = [];
        foreach (Tuple<string, string> preset in presets)
        {
            string presetKey = preset.Item2;
            items.Add(new MenuFlyoutToggleItemViewModel(preset.Item1)
            {
                IsChecked = _model.PresetKey == presetKey,
                OnClick = () =>
                {
                    if (presetKey != _model.PresetKey && _comic is not null)
                    {
                        _model.PresetKey = presetKey;
                        _model.ToComic(_comic);
                        _model = ReaderSettingDataModel.FromComic(_comic);
                        DispatchDataChangeEvent();
                    }

                    UpdateUI();
                },
            });
        }

        var flyout = new MenuFlyout();
        foreach (BaseMenuFlyoutItemViewModel item in items)
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

    private static int PageArrangementToIndex(PageArrangementEnum pageArrangement)
    {
        switch (pageArrangement)
        {
            case PageArrangementEnum.Single:
                return 0;
            case PageArrangementEnum.DualCover:
                return 1;
            case PageArrangementEnum.DualCoverMirror:
                return 2;
            case PageArrangementEnum.DualNoCover:
                return 3;
            case PageArrangementEnum.DualNoCoverMirror:
                return 4;
            default:
                Logger.AssertNotReachHere("979D38CE673E1BC0");
                return 0;
        }
    }

    private static PageArrangementEnum IndexToPageArrangement(int index)
    {
        switch (index)
        {
            case 0:
                return PageArrangementEnum.Single;
            case 1:
                return PageArrangementEnum.DualCover;
            case 2:
                return PageArrangementEnum.DualCoverMirror;
            case 3:
                return PageArrangementEnum.DualNoCover;
            case 4:
                return PageArrangementEnum.DualNoCoverMirror;
            default:
                Logger.AssertNotReachHere("B8CA81937666C2FB");
                return PageArrangementEnum.Single;
        }
    }
}
