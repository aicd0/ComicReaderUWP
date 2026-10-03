// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.HotKey;
using ComicReaderUWP.Common.Localization;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

using Windows.System;
using Windows.UI.Core;

namespace ComicReaderUWP.UserControls.Misc;

internal sealed partial class KeyboardShortcutRecorder : BaseUserControl
{
    public delegate void ShortcutChangedEventHandler(KeyboardShortcutRecorder sender, KeyboardShortcutModel model);
    public event ShortcutChangedEventHandler? ShortcutChanged;

    public KeyboardShortcutRecorder()
    {
        InitializeComponent();
        UpdateDisplay();
    }

    public KeyboardShortcutModel? Shortcut { get; private set; }

    public void SetShortcut(KeyboardShortcutModel? shortcut)
    {
        Shortcut = shortcut;
        UpdateDisplay();
    }

    private void OnTapped(object sender, TappedRoutedEventArgs e)
    {
        Focus(FocusState.Programmatic);
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        VirtualKey key = e.Key;
        if (key == VirtualKey.Escape)
        {
            return;
        }

        e.Handled = true;

        if (!IsModifierKey(key))
        {
            KeyboardShortcutModel model = new()
            {
                Key = key,
                Modifiers = GetCurrentModifiers(),
            };
            Shortcut = model;
            ShortcutChanged?.Invoke(this, model);
        }

        UpdateDisplay();
    }

    private void OnPreviewKeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (IsModifierKey(e.Key))
        {
            UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        if (Shortcut is not null)
        {
            ValueTextBlock.Text = Shortcut.ToString();
            ValueTextBlock.Visibility = Visibility.Visible;
            PlaceholderTextBlock.Visibility = Visibility.Collapsed;
            return;
        }

        VirtualKeyModifiers modifiers = GetCurrentModifiers();
        PlaceholderTextBlock.Text = modifiers == VirtualKeyModifiers.None
            ? StringResourceProvider.Instance.PressKeyCombination
            : KeyboardShortcutModel.FormatModifiers(modifiers);
        PlaceholderTextBlock.Visibility = Visibility.Visible;
        ValueTextBlock.Visibility = Visibility.Collapsed;
    }

    private static bool IsModifierKey(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.Control or
            VirtualKey.LeftControl or
            VirtualKey.RightControl or
            VirtualKey.Shift or
            VirtualKey.LeftShift or
            VirtualKey.RightShift or
            VirtualKey.Menu or
            VirtualKey.LeftMenu or
            VirtualKey.RightMenu or
            VirtualKey.LeftWindows or
            VirtualKey.RightWindows => true,
            _ => false,
        };
    }

    private static VirtualKeyModifiers GetCurrentModifiers()
    {
        VirtualKeyModifiers modifiers = VirtualKeyModifiers.None;

        if (IsKeyDown(VirtualKey.Control))
        {
            modifiers |= VirtualKeyModifiers.Control;
        }

        if (IsKeyDown(VirtualKey.Shift))
        {
            modifiers |= VirtualKeyModifiers.Shift;
        }

        if (IsKeyDown(VirtualKey.Menu))
        {
            modifiers |= VirtualKeyModifiers.Menu;
        }

        if (IsKeyDown(VirtualKey.LeftWindows) || IsKeyDown(VirtualKey.RightWindows))
        {
            modifiers |= VirtualKeyModifiers.Windows;
        }

        return modifiers;
    }

    private static bool IsKeyDown(VirtualKey key)
    {
        CoreVirtualKeyStates state = InputKeyboardSource.GetKeyStateForCurrentThread(key);
        return state.HasFlag(CoreVirtualKeyStates.Down);
    }
}
