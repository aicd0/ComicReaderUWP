// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.BaseUI;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Views.Pages.Settings;

internal sealed partial class GeneralSettingsView : BaseUserControl
{
    public GeneralSettingsViewModel ViewModel { get; } = new();

    public GeneralSettingsView()
    {
        InitializeComponent();
    }

    public void Initialize(SettingsSharedViewModel shared)
    {
        ViewModel.Initialize(shared);
    }

    private void CloseLastTabBehaviorBehaviorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SetCloseLastTabBehavior(((ComboBox)sender).SelectedIndex);
    }

    private void OpenComicDefaultBehaviorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SetOpenComicDefaultBehavior(((ComboBox)sender).SelectedIndex);
    }
}
