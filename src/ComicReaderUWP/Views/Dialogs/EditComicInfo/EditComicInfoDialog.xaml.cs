// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Views.Dialogs.EditComicInfo;

internal sealed partial class EditComicInfoDialog : BaseContentDialog
{
    public EditComicInfoDialogViewModel ViewModel = new();

    public EditComicInfoDialog(IEnumerable<ComicModel> comics)
    {
        InitializeComponent();

        TabSelectorBar.SelectedItem = GeneralSelectorBarItem;

        ViewModel.Initialize(comics);
    }

    //
    // Lifecycle
    //

    protected override void OnStart()
    {
        base.OnStart();

        CoverImageField.WindowId = WindowId;
        CoverImageField.Changed += CoverImageField_Changed;
        CoverImageField.Initialize(ViewModel.CoverImageUri);

        BackgroundImageField.WindowId = WindowId;
        BackgroundImageField.Changed += BackgroundImageField_Changed;
        BackgroundImageField.Initialize(ViewModel.BackgroundImageUri);
    }

    //
    // Events
    //

    private void TabSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        bool showImages = sender.SelectedItem == ImagesSelectorBarItem;
        GeneralTabGrid.Visibility = showImages ? Visibility.Collapsed : Visibility.Visible;
        ImagesTabGrid.Visibility = showImages ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DoneButton_Click(object sender, RoutedEventArgs args)
    {
        CoroutineUtils.Run(async () =>
        {
            await ViewModel.Save();
            Hide();
        });
    }

    private void CancelButton_Click(object sender, RoutedEventArgs args)
    {
        Hide();
    }

    private void OnShowTagInfoButtonClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.IsTagInfoBarOpen = !ViewModel.IsTagInfoBarOpen;
    }

    private void OnTagInfoBarCloseButtonClicked(Microsoft.UI.Xaml.Controls.InfoBar sender, object args)
    {
        ViewModel.IsTagInfoBarOpen = false;
    }

    private void Title1TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.SetTitle1(((TextBox)sender).Text);
    }

    private void Title2TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.SetTitle2(((TextBox)sender).Text);
    }

    private void DescriptionTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.SetDescription(((TextBox)sender).Text);
    }

    private void RatingTextBox_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        string text = args.NewText;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (!RatingRegex().IsMatch(text))
        {
            args.Cancel = true;
        }
    }

    private void RatingTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.SetRating(((TextBox)sender).Text);
    }

    private void RatingPercentageCheckBox_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SetRatingPercentageEnabled(((CheckBox)sender).IsChecked ?? false);
    }

    private void TagTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.SetTags(((TextBox)sender).Text);
    }

    private void TagDiffModeCheckBox_Click(object sender, RoutedEventArgs e)
    {
        bool isChecked = ((CheckBox)sender).IsChecked == true;
        ViewModel.SetTagDiffMode(isChecked);
        if (isChecked)
        {
            TagIdCheckBox.IsEnabled = true;
        }
        else
        {
            TagIdCheckBox.IsChecked = false;
            TagIdCheckBox.IsEnabled = false;
            ViewModel.SetTagIdMode(false);
        }
    }

    private void TagIdCheckBox_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SetTagIdMode(((CheckBox)sender).IsChecked == true);
    }

    private void ClearReaderSettingsCheckBox_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SetClearReaderSettings(((CheckBox)sender).IsChecked == true);
    }

    private void CoverImageField_Changed(object? sender, EventArgs e)
    {
        ViewModel.SetCoverImage(CoverImageField.PendingFilePath);
    }

    private void BackgroundImageField_Changed(object? sender, EventArgs e)
    {
        ViewModel.SetBackgroundImage(BackgroundImageField.PendingFilePath);
    }

    private void OpenMetadataFolderButton_Click(object sender, RoutedEventArgs e)
    {
        CoroutineUtils.Run(ViewModel.OpenMetadataFolder);
    }

    //
    // Misc
    //

    [GeneratedRegex(@"^\d*\.?\d*$")]
    private static partial Regex RatingRegex();
}
