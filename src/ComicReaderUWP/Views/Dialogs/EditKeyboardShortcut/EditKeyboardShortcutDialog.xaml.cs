// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.HotKey;
using ComicReaderUWP.UserControls.Misc;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Views.Dialogs.EditKeyboardShortcut;

internal sealed partial class EditKeyboardShortcutDialog : BaseContentDialog
{
    public EditKeyboardShortcutDialogViewModel ViewModel { get; } = new();

    public EditKeyboardShortcutDialog(KeyboardShortcutModel? original)
    {
        InitializeComponent();

        ViewModel.Initialize(original);

        if (original is not null)
        {
            Recorder.SetShortcut(original);
            ViewModel.UpdateShortcut(original);
        }

        Recorder.ShortcutChanged += OnRecorderShortcutChanged;
        Opened += OnDialogOpened;
    }

    private void OnDialogOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        Recorder.Focus(FocusState.Programmatic);
    }

    private void OnRecorderShortcutChanged(KeyboardShortcutRecorder sender, KeyboardShortcutModel model)
    {
        ViewModel.UpdateShortcut(model);
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CreateResult() is not null)
        {
            Hide();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void ActionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.ActionIndex = ((ComboBox)sender).SelectedIndex;
    }
}
