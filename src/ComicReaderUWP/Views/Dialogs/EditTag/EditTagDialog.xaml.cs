// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Views.Dialogs.EditFilter;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Views.Dialogs.EditTag;

internal sealed partial class EditTagDialog : BaseContentDialog
{
    public EditTagDialogViewModel ViewModel = new();

    public EditTagDialog(string tagCategory, string tag)
    {
        InitializeComponent();

        ViewModel.Initialize(tagCategory, tag);
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

    private void TagCategoryNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.UpdateTagCategoryName(((TextBox)sender).Text ?? "");
    }

    private void TagNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.UpdateTagName(((TextBox)sender).Text ?? "");
    }

    private void DescriptionTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.UpdateDescription(((TextBox)sender).Text ?? "");
    }

    private void LinksTipButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ThirdPartyLauncher.StartTemporaryTextFile("tag_link_reference.txt", StringResource.TagLinkTip);
    }
}
