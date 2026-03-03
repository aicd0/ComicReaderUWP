// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.BaseUI;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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

    private void RestoreLastReadingPositionCheckBox_Click(object sender, RoutedEventArgs e)
    {
        bool? isChecked = ((CheckBox)sender).IsChecked;
        if (isChecked.HasValue)
        {
            ViewModel.SetRestoreLastReadingPosition(isChecked.Value);
        }
    }

    private void UseScrollingAreaAsStartEndCheckBox_Click(object sender, RoutedEventArgs e)
    {
        bool? isChecked = ((CheckBox)sender).IsChecked;
        if (isChecked.HasValue)
        {
            ViewModel.SetUseScrollingAreaAsStartEnd(isChecked.Value);
        }
    }

    private void HideCursorAutomaticallyCheckBox_Click(object sender, RoutedEventArgs e)
    {
        bool? isChecked = ((CheckBox)sender).IsChecked;
        if (isChecked.HasValue)
        {
            ViewModel.SetHideCursorAutomatically(isChecked.Value);
        }
    }

    private void KeepScreenOnBehaviorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SetKeepScreenOnBehavior(((ComboBox)sender).SelectedIndex);
    }
}
