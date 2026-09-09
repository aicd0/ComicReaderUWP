// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Misc;

using Microsoft.UI.Xaml;
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

    private void ClearAllHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Run(() => ComicHistoryItemModel.ClearAsync());
        ViewModel.IsClearHistoryEnabled = false;
    }

    private void SaveBrowsingHistoryCheckBox_Click(object sender, RoutedEventArgs e)
    {
        bool? isChecked = ((CheckBox)sender).IsChecked;
        if (isChecked.HasValue)
        {
            AppSettingsModel.SaveBrowsingHistory = isChecked.Value;
        }
    }

    private void EnableCompressedFileCacheCheckBox_Click(object sender, RoutedEventArgs e)
    {
        bool? isChecked = ((CheckBox)sender).IsChecked;
        if (isChecked.HasValue)
        {
            AppSettingsModel.EnableCompressedFileCache = isChecked.Value;
        }
    }
}
