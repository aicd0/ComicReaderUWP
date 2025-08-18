// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using ComicReader.Common.BaseUI;
using ComicReader.Common.Utils;
using ComicReader.Data.Models;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Dialogs.EditFilter;

internal sealed partial class EditFilterDialog : BaseContentDialog
{
    public EditFilterDialogViewModel ViewModel = new();

    public EditFilterDialog(ComicFilterModel.ExternalFilterModel filter)
    {
        InitializeComponent();

        ViewModel.Initialize(filter);
    }

    protected override void OnStart()
    {
        base.OnStart();

        ObserveData();
    }

    private void ObserveData()
    {
        ViewModel.NameLiveData.ObserveSticky(this, delegate (string text)
        {
            NameTextBox.Text = text ?? "";
        });

        ViewModel.ExpressionLiveData.ObserveSticky(this, delegate (string text)
        {
            ExpressionTextBox.Text = text ?? "";
        });

        ViewModel.ParseResultLiveData.ObserveSticky(this, delegate (string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                ParseResultTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            }
            else
            {
                ParseResultTextBlock.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                ParseResultTextBlock.Text = text ?? "";
            }
        });

        ViewModel.SaveEnableLiveData.ObserveSticky(this, delegate (bool enabled)
        {
            SaveButton.IsEnabled = enabled;
        });

        ViewModel.SaveAsNewEnableLiveData.ObserveSticky(this, delegate (bool enabled)
        {
            SaveAsNewButton.IsEnabled = enabled;
        });

        ViewModel.AppendToExpressionLiveData.Observe(this, info =>
        {
            string existingText = ExpressionTextBox.Text;
            int selectionStart = ExpressionTextBox.SelectionStart;
            int selectionLength = ExpressionTextBox.SelectionLength;
            StringBuilder newText = new(existingText);
            if (existingText.Length > 0 && selectionStart > 0 && !char.IsWhiteSpace(existingText[selectionStart - 1]))
            {
                newText.Insert(selectionStart, ' ');
                selectionStart++;
            }
            newText.Remove(selectionStart, selectionLength);
            newText.Insert(selectionStart, info.Text);
            int selectionEnd = selectionStart + info.Text.Length;
            if (info.Text.Length > 0 && !char.IsWhiteSpace(info.Text[^1]) && selectionEnd < newText.Length && !char.IsWhiteSpace(newText[selectionEnd]))
            {
                newText.Insert(selectionEnd, ' ');
            }
            ExpressionTextBox.Text = newText.ToString();
            ExpressionTextBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            ExpressionTextBox.SelectionStart = selectionStart + (info.CursorPosition >= 0 ? info.CursorPosition : info.Text.Length + info.CursorPosition + 1);
        });
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

    private void ExpressionTipButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ThirdPartyLauncher.StartTemporaryTextFile("expression_reference.txt", StringResource.ExpressionReference);
    }

    private void NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.UpdateName(NameTextBox.Text ?? "");
    }

    private void ExpressionTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.UpdateExpression(ExpressionTextBox.Text ?? "");
    }
}
