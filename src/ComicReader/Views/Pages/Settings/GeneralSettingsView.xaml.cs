// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.BaseUI;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Pages.Settings;

internal sealed partial class GeneralSettingsView : BaseUserControl
{
    public GeneralSettingsViewModel ViewModel { get; } = new();

    public GeneralSettingsView()
    {
        InitializeComponent();

        DataContextChanged += (s, e) =>
        {
            Bindings.StopTracking();
            Bindings.Update();
        };
    }

    public void Initialize()
    {
        ViewModel.Initialize();
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
