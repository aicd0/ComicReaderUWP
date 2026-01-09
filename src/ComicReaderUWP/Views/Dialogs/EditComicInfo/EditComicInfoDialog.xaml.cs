// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.RegularExpressions;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.SDK.Common.Utils;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Views.Dialogs.EditComicInfo;

internal sealed partial class EditComicInfoDialog : BaseContentDialog
{
    public EditComicInfoDialogViewModel ViewModel = new();

    public EditComicInfoDialog(IEnumerable<ComicModel> comics)
    {
        InitializeComponent();

        ViewModel.Initialize(comics);
    }

    //
    // Lifecycle
    //

    protected override void OnStart()
    {
        base.OnStart();

        ObserveData();
    }

    private void ObserveData()
    {
        ViewModel.Title1TextLiveData.ObserveSticky(this, (text) =>
        {
            Title1TextBox.Text = text;
        });

        ViewModel.Title2TextLiveData.ObserveSticky(this, (text) =>
        {
            Title2TextBox.Text = text;
        });

        ViewModel.DescriptionTextLiveData.ObserveSticky(this, (text) =>
        {
            DescriptionTextBox.Text = text;
        });

        ViewModel.TagTextLiveData.ObserveSticky(this, (text) =>
        {
            TagTextBox.Text = text;
        });

        ViewModel.Title1ChangedLiveData.ObserveSticky(this, (changed) =>
        {
            string name = StringResource.Title1;
            if (changed)
            {
                name += " *";
            }

            Title1NameTextBlock.Text = name;
        });

        ViewModel.Title2ChangedLiveData.ObserveSticky(this, (changed) =>
        {
            string name = StringResource.Title2;
            if (changed)
            {
                name += " *";
            }

            Title2NameTextBlock.Text = name;
        });

        ViewModel.DescriptionChangedLiveData.ObserveSticky(this, (changed) =>
        {
            string name = StringResource.Description;
            if (changed)
            {
                name += " *";
            }

            DescriptionNameTextBlock.Text = name;
        });

        ViewModel.TagChangedLiveData.ObserveSticky(this, (changed) =>
        {
            string name = StringResource.Tags;
            if (changed)
            {
                name += " *";
            }

            TagNameTextBlock.Text = name;
        });
    }

    //
    // Events
    //

    private void ContentDialogPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ViewModel.Save();
    }

    private void ContentDialogSecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
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

    //
    // Misc
    //

    [GeneratedRegex(@"^\d*\.?\d*$")]
    private static partial Regex RatingRegex();
}
