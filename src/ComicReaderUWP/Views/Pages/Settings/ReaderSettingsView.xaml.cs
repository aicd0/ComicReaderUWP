// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Data.Models.Misc;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace ComicReaderUWP.Views.Pages.Settings;

internal sealed partial class ReaderSettingsView : BaseUserControl
{
    public ReaderSettingsViewModel ViewModel { get; } = new();

    public ReaderSettingsView()
    {
        InitializeComponent();
    }

    public void Initialize(SettingsSharedViewModel shared)
    {
        ViewModel.Initialize(shared);
    }

    private void TransitionAnimationCheckBox_Click(object sender, RoutedEventArgs e)
    {
        bool? isChecked = ((CheckBox)sender).IsChecked;
        if (isChecked.HasValue)
        {
            AppSettingsModel.TransitionAnimation = isChecked.Value;
        }
    }

    private void RestoreLastReadingPositionCheckBox_Click(object sender, RoutedEventArgs e)
    {
        bool? isChecked = ((CheckBox)sender).IsChecked;
        if (isChecked.HasValue)
        {
            AppSettingsModel.RestoreLastReadingPosition = isChecked.Value;
        }
    }

    private void RestoreLastReadingPositionOnlyAppliesToReadingComicsCheckBox_Click(object sender, RoutedEventArgs e)
    {
        bool? isChecked = ((CheckBox)sender).IsChecked;
        if (isChecked.HasValue)
        {
            AppSettingsModel.RestoreLastReadingPositionOnlyAppliesToReadingComics = isChecked.Value;
        }
    }

    private void UseScrollingAreaAsStartEndCheckBox_Click(object sender, RoutedEventArgs e)
    {
        bool? isChecked = ((CheckBox)sender).IsChecked;
        if (isChecked.HasValue)
        {
            AppSettingsModel.UseScrollingAreaAsStartEnd = isChecked.Value;
        }
    }

    private void AutoToggleOverlaysOnCursorCheckBox_Click(object sender, RoutedEventArgs e)
    {
        bool? isChecked = ((CheckBox)sender).IsChecked;
        if (isChecked.HasValue)
        {
            AppSettingsModel.AutoToggleOverlaysOnCursor = isChecked.Value;
        }
    }

    private void HideCursorAutomaticallyCheckBox_Click(object sender, RoutedEventArgs e)
    {
        bool? isChecked = ((CheckBox)sender).IsChecked;
        if (isChecked.HasValue)
        {
            AppSettingsModel.AutoHideCursor = isChecked.Value;
        }
    }

    private void PreloadPagesAfterSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        int value = Math.Clamp((int)e.NewValue, 0, 10);
        ViewModel.SetPreloadPagesAfter(value);
    }

    private void PreloadPagesBeforeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        int value = Math.Clamp((int)e.NewValue, 0, 10);
        ViewModel.SetPreloadPagesBefore(value);
    }

    private void KeepScreenOnBehaviorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SetKeepScreenOnBehavior(((ComboBox)sender).SelectedIndex);
    }
}
