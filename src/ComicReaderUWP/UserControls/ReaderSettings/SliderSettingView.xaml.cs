// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReaderUWP.Common.BaseUI;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace ComicReaderUWP.UserControls.ReaderSettings;

internal sealed partial class SliderSettingView : BaseUserControl
{
    public event EventHandler? ValueChanged;

    private bool _isSyncingValue;

    public SliderSettingView()
    {
        InitializeComponent();

        Entry.ValueChanged += Entry_ValueChanged;
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
        new PropertyMetadata(string.Empty, OnLabelChanged));

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SliderSettingView)d;
        control.Entry.Label = (string)e.NewValue;
    }

    //
    // Editor
    //

    public double Value
    {
        get => Entry.Value;
        set => Entry.Value = value;
    }

    public double RestoreValue
    {
        get => Entry.RestoreValue;
        set => Entry.RestoreValue = value;
    }

    public double ResetValue
    {
        get => Entry.ResetValue;
        set => Entry.ResetValue = value;
    }

    public double Maximum
    {
        get => MainSlider.Maximum;
        set => MainSlider.Maximum = value;
    }

    public double Minimum
    {
        get => MainSlider.Minimum;
        set => MainSlider.Minimum = value;
    }

    public SliderSnapsTo SnapsTo
    {
        get => MainSlider.SnapsTo;
        set => MainSlider.SnapsTo = value;
    }

    public double StepFrequency
    {
        get => MainSlider.StepFrequency;
        set => MainSlider.StepFrequency = value;
    }

    public double TickFrequency
    {
        get => MainSlider.TickFrequency;
        set => MainSlider.TickFrequency = value;
    }

    public TickPlacement TickPlacement
    {
        get => MainSlider.TickPlacement;
        set => MainSlider.TickPlacement = value;
    }

    //
    // Events
    //

    private void MainSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isSyncingValue)
        {
            return;
        }

        _isSyncingValue = true;
        try
        {
            Entry.Value = MainSlider.Value;
        }
        finally
        {
            _isSyncingValue = false;
        }

        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Entry_ValueChanged(object? sender, EventArgs e)
    {
        if (_isSyncingValue)
        {
            return;
        }

        _isSyncingValue = true;
        try
        {
            if (MainSlider.Value != Entry.Value)
            {
                MainSlider.Value = Entry.Value;
            }
        }
        finally
        {
            _isSyncingValue = false;
        }

        ValueChanged?.Invoke(this, EventArgs.Empty);
    }
}
