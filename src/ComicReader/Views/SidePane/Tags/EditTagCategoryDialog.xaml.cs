// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.BaseUI;
using ComicReader.Common.Utils;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.SidePane.Tags;

internal sealed partial class EditTagCateogoryDialog : BaseContentDialog
{
    public EditTagCateogoryDialogViewModel ViewModel = new();

    public EditTagCateogoryDialog(string tagCategory)
    {
        InitializeComponent();

        ViewModel.Initialize(tagCategory);
    }

    private void ContentDialog_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ObserveData();
    }

    private void ObserveData()
    {
        ViewModel.NameLiveData.ObserveSticky(this, delegate (string text)
        {
            NameTextBox.Text = text ?? "";
        });

        ViewModel.SaveEnableLiveData.ObserveSticky(this, delegate (bool enabled)
        {
            SaveButton.IsEnabled = enabled;
        });
    }

    //
    // Events
    //

    private void CancelButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Hide();
    }

    private void SaveButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.Save();
        Hide();
    }

    private void NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.UpdateName(NameTextBox.Text ?? "");
    }
}
