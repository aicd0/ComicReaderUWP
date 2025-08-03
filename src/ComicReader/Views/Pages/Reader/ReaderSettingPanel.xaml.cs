// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.Common.BaseUI;
using ComicReader.Data.Models;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.Views.Pages.Navigation;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Pages.Reader;

internal sealed partial class ReaderSettingPanel : BaseUserControl
{
    public delegate void DataChangedEventHandler(ReaderSettingDataModel data);
    public event DataChangedEventHandler? DataChanged;

    private ReaderSettingDataModel _model = new();
    private bool _updatingUI = false;

    public ReaderSettingPanel()
    {
        InitializeComponent();
    }

    public void SetData(ReaderSettingDataModel settings)
    {
        _model = settings;
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private int PageArrangementToIndex(PageArrangementEnum pageArrangement)
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

    private PageArrangementEnum IndexToPageArrangement(int index)
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
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private void AbbVertical_Click(object sender, RoutedEventArgs e)
    {
        _model.IsVertical = false;
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private void AbbHorizontal_Click(object sender, RoutedEventArgs e)
    {
        _model.IsVertical = true;
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private void AbbLeftToRight_Click(object sender, RoutedEventArgs e)
    {
        _model.IsLeftToRight = false;
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private void AbbRightToLeft_Click(object sender, RoutedEventArgs e)
    {
        _model.IsLeftToRight = true;
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private void AbbSeperate_Click(object sender, RoutedEventArgs e)
    {
        _model.IsContinuous = true;
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private void AbbContinuous_Click(object sender, RoutedEventArgs e)
    {
        _model.IsContinuous = false;
        UpdateUI();
        DispatchDataChangeEvent();
    }

    private void PageGapSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        _model.PageGap = Math.Clamp((int)e.NewValue, 0, 200);
        DispatchDataChangeEvent();
    }

    private void SaveAsDefaultToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        _model.UseDefault = SaveAsDefaultToggleSwitch.IsOn;
        DispatchDataChangeEvent();
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

        SaveAsDefaultToggleSwitch.IsOn = _model.UseDefault;
        PageGapSlider.Value = Math.Clamp(_model.PageGap, 0, 200);
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
