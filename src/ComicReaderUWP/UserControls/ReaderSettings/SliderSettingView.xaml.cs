// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.BaseUI;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace ComicReaderUWP.UserControls.ReaderSettings;

internal sealed partial class SliderSettingView : BaseUserControl
{
    public event RangeBaseValueChangedEventHandler? ValueChanged;

    private bool _isOneRow = true;

    public SliderSettingView()
    {
        InitializeComponent();

        UpdateRestoreButtonVisibility();

        MainSlider.ValueChanged += (s, e) =>
        {
            ValueChanged?.Invoke(this, e);
        };
    }

    //
    // Dependency Properties
    //

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label),
        typeof(string),
        typeof(SliderSettingView),
        new PropertyMetadata(string.Empty));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(SliderSettingView),
        new PropertyMetadata(0.0, OnValueChanged));

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SliderSettingView)d;
        control.UpdateRestoreButtonVisibility();
    }

    public double DefaultValue
    {
        get => (double)GetValue(DefaultValueProperty);
        set => SetValue(DefaultValueProperty, value);
    }

    public static readonly DependencyProperty DefaultValueProperty = DependencyProperty.Register(
        nameof(DefaultValue),
        typeof(double),
        typeof(SliderSettingView),
        new PropertyMetadata(0.0, OnDefaultValueChanged));

    private static void OnDefaultValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SliderSettingView)d;
        control.UpdateRestoreButtonVisibility();
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum),
        typeof(double),
        typeof(SliderSettingView),
        new PropertyMetadata(100.0));

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum),
        typeof(double),
        typeof(SliderSettingView),
        new PropertyMetadata(0.0));

    public SliderSnapsTo SnapsTo
    {
        get => (SliderSnapsTo)GetValue(SnapsToProperty);
        set => SetValue(SnapsToProperty, value);
    }

    public static readonly DependencyProperty SnapsToProperty = DependencyProperty.Register(
        nameof(SnapsTo),
        typeof(SliderSnapsTo),
        typeof(SliderSettingView),
        new PropertyMetadata(SliderSnapsTo.StepValues));

    public double StepFrequency
    {
        get => (double)GetValue(StepFrequencyProperty);
        set => SetValue(StepFrequencyProperty, value);
    }

    public static readonly DependencyProperty StepFrequencyProperty = DependencyProperty.Register(
        nameof(StepFrequency),
        typeof(double),
        typeof(SliderSettingView),
        new PropertyMetadata(1.0));

    public double TickFrequency
    {
        get => (double)GetValue(TickFrequencyProperty);
        set => SetValue(TickFrequencyProperty, value);
    }

    public static readonly DependencyProperty TickFrequencyProperty = DependencyProperty.Register(
        nameof(TickFrequency),
        typeof(double),
        typeof(SliderSettingView),
        new PropertyMetadata(1.0));

    public TickPlacement TickPlacement
    {
        get => (TickPlacement)GetValue(TickPlacementProperty);
        set => SetValue(TickPlacementProperty, value);
    }

    public static readonly DependencyProperty TickPlacementProperty = DependencyProperty.Register(
        nameof(TickPlacement),
        typeof(TickPlacement),
        typeof(SliderSettingView),
        new PropertyMetadata(TickPlacement.None));

    //
    // Events
    //

    private void LabelStackPanel_Tapped(object sender, TappedRoutedEventArgs e)
    {
        bool isOneRow = !_isOneRow;
        if (VisualStateManager.GoToState(this, isOneRow ? "OneRow" : "TwoRows", true))
        {
            _isOneRow = isOneRow;
            UpdateRestoreButtonVisibility();
        }
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        Value = DefaultValue;
    }

    //
    // UI
    //

    private void UpdateRestoreButtonVisibility()
    {
        bool visible = !_isOneRow && Value != DefaultValue;
        RestoreButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }
}
