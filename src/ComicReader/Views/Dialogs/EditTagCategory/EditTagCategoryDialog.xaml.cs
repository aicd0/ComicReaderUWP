// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.BaseUI;
using ComicReader.Views.Dialogs.EditFilter;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Dialogs.EditTagCategory;

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
        ViewModel.UpdateName(((TextBox)sender).Text ?? "");
    }

    private void LinksTipButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ThirdPartyLauncher.StartTemporaryTextFile("tag_link_reference.txt", StringResource.TagLinkTip);
    }
}
