// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReaderUWP.Common.BaseUI;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace ComicReaderUWP.UserControls.ReaderSettings;

internal sealed partial class SettingEntryView : BaseUserControl
{
    public event EventHandler? ValueChanged;

    private bool _isExpanded;

    public SettingEntryView()
    {
        InitializeComponent();

        UpdateLabelText();
        UpdateExpandedState();
    }

    //
    // Dependency Properties
    //

    public object? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(
        nameof(Editor),
        typeof(object),
        typeof(SettingEntryView),
        new PropertyMetadata(null, OnEditorChanged));

    private static void OnEditorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SettingEntryView)d;
        control.EditorHost.Content = e.NewValue;
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label),
        typeof(string),
        typeof(SettingEntryView),
        new PropertyMetadata(string.Empty, OnLabelChanged));

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SettingEntryView)d;
        control.UpdateLabelText();
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(SettingEntryView),
        new PropertyMetadata(0.0, OnValueChanged));

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SettingEntryView)d;
        control.UpdateLabelText();
        control.UpdateLinkVisibility();
        control.ValueChanged?.Invoke(control, EventArgs.Empty);
    }

    public double RestoreValue
    {
        get => (double)GetValue(RestoreValueProperty);
        set => SetValue(RestoreValueProperty, value);
    }

    public static readonly DependencyProperty RestoreValueProperty = DependencyProperty.Register(
        nameof(RestoreValue),
        typeof(double),
        typeof(SettingEntryView),
        new PropertyMetadata(0.0, OnRestoreValueChanged));

    private static void OnRestoreValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SettingEntryView)d;
        control.UpdateLabelText();
        control.UpdateLinkVisibility();
    }

    public double ResetValue
    {
        get => (double)GetValue(ResetValueProperty);
        set => SetValue(ResetValueProperty, value);
    }

    public static readonly DependencyProperty ResetValueProperty = DependencyProperty.Register(
        nameof(ResetValue),
        typeof(double),
        typeof(SettingEntryView),
        new PropertyMetadata(0.0, OnResetValueChanged));

    private static void OnResetValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SettingEntryView)d;
        control.UpdateLinkVisibility();
    }

    public double RowHeight
    {
        get => (double)GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    public static readonly DependencyProperty RowHeightProperty = DependencyProperty.Register(
        nameof(RowHeight),
        typeof(double),
        typeof(SettingEntryView),
        new PropertyMetadata(30.0, OnRowHeightChanged));

    private static void OnRowHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SettingEntryView)d;
        control.UpdateExpandedState();
    }

    /// <summary>
    /// The width of the column the editor sits in while collapsed. A slider asks for the width it was last laid out
    /// at, so it needs a width of its own there; NaN lets the editor size the column itself.
    /// </summary>
    public double EditorWidth
    {
        get => (double)GetValue(EditorWidthProperty);
        set => SetValue(EditorWidthProperty, value);
    }

    public static readonly DependencyProperty EditorWidthProperty = DependencyProperty.Register(
        nameof(EditorWidth),
        typeof(double),
        typeof(SettingEntryView),
        new PropertyMetadata(double.NaN, OnEditorWidthChanged));

    private static void OnEditorWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SettingEntryView)d;
        control.UpdateExpandedState();
    }

    public bool ShowValue
    {
        get => (bool)GetValue(ShowValueProperty);
        set => SetValue(ShowValueProperty, value);
    }

    public static readonly DependencyProperty ShowValueProperty = DependencyProperty.Register(
        nameof(ShowValue),
        typeof(bool),
        typeof(SettingEntryView),
        new PropertyMetadata(false, OnShowValueChanged));

    private static void OnShowValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SettingEntryView)d;
        control.UpdateLabelText();
    }

    //
    // Events
    //

    private void LabelStackPanel_Tapped(object sender, TappedRoutedEventArgs e)
    {
        _isExpanded = !_isExpanded;
        UpdateExpandedState();
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        Value = RestoreValue;
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        Value = ResetValue;
    }

    //
    // UI
    //

    private void UpdateExpandedState()
    {
        Column0.Width = _isExpanded ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        Column1.Width = _isExpanded ? new GridLength(0) : new GridLength(12);
        Column2.Width = _isExpanded ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Column3.Width = !_isExpanded && !double.IsNaN(EditorWidth) ? new GridLength(EditorWidth) : GridLength.Auto;
        Row0.Height = _isExpanded ? GridLength.Auto : new GridLength(RowHeight);
        Row1.Height = _isExpanded ? new GridLength(RowHeight) : new GridLength(0);
        ExpandIcon.Glyph = _isExpanded ? "\uE70D" : "\uE76C";
        Grid.SetRow(EditorHost, _isExpanded ? 1 : 0);
        Grid.SetColumn(EditorHost, _isExpanded ? 0 : 3);
        Grid.SetColumnSpan(EditorHost, _isExpanded ? 4 : 1);
        UpdateLinkVisibility();
    }

    private void UpdateLinkVisibility()
    {
        bool visible = _isExpanded;
        RestoreButton.Visibility = visible && Value != RestoreValue ? Visibility.Visible : Visibility.Collapsed;
        ResetButton.Visibility = visible && Value != ResetValue ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateLabelText()
    {
        string text = ShowValue ? $"{Label} ({Value:0})" : Label;
        LabelTextBlock.Text = Value != RestoreValue ? $"{text} *" : text;
    }
}
