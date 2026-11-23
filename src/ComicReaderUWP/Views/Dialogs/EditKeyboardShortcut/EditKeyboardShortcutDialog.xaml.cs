// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.HotKey;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;
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

        MenuFlyout actionMenu = new();
        actionMenu.Opening += ActionMenu_Opening;
        ActionButton.Flyout = actionMenu;

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

    private void ActionMenu_Opening(object? sender, object args)
    {
        if (sender is not MenuFlyout flyout)
        {
            return;
        }

        flyout.Items.Clear();
        foreach (BaseMenuFlyoutItemModel item in CreateActionMenuItems(ViewModel.ActionTree))
        {
            flyout.Items.Add(item.CreateMenuFlyoutItem());
        }
    }

    private List<BaseMenuFlyoutItemModel> CreateActionMenuItems(KeyboardShortcutActionGroup group)
    {
        List<BaseMenuFlyoutItemModel> items = [];
        foreach (KeyboardShortcutActionEntry entry in group.Entries)
        {
            KeyboardShortcutActionEntry choice = entry;
            items.Add(new ToggleMenuFlyoutItemModel
            {
                Text = choice.Name,
                IsChecked = choice == ViewModel.SelectedAction,
                Click = () => ViewModel.SelectedAction = choice,
            });
        }

        foreach (KeyboardShortcutActionGroup child in group.Groups)
        {
            items.Add(new SubItemMenuFlyoutItemModel
            {
                Text = child.Name,
                Items = CreateActionMenuItems(child),
            });
        }

        return items;
    }
}
