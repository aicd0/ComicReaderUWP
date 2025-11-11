// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.BaseUI;
using ComicReader.Data.Models.Comic;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Dialogs.EditReaderSettingPreset;

internal sealed partial class EditReaderSettingPresetDialog : BaseContentDialog
{
    public EditReaderSettingPresetDialogViewModel ViewModel = new();

    public EditReaderSettingPresetDialog(ComicModel comic)
    {
        InitializeComponent();

        ViewModel.Initialize(comic);
    }

    //
    // Events
    //

    private void CancelButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Hide();
    }

    private void DeleteButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.Delete();
        Hide();
    }

    private void SaveButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.Save();
        Hide();
    }

    private void SaveAsNewButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.SaveAsNew();
        Hide();
    }

    private void NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var textBox = (TextBox)sender;
        ViewModel.UpdateName(textBox.Text ?? string.Empty);
    }
}
