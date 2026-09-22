// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.BaseUI;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Views.Pages.Settings;

internal sealed partial class AppearanceSettingsView : BaseUserControl
{
    public AppearanceSettingsViewModel ViewModel { get; } = new();

    public AppearanceSettingsView()
    {
        InitializeComponent();
    }

    public void Initialize(SettingsSharedViewModel shared)
    {
        ViewModel.Initialize(shared);
    }

    private void BackgroundComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SetBackground(((ComboBox)sender).SelectedIndex);
    }

    private void AppearanceRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SetAppearance(((RadioButtons)sender).SelectedIndex);
    }
}
