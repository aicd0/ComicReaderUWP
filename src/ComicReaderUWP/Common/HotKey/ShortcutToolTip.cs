// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Common.HotKey;

public static class ShortcutToolTip
{
    public static readonly DependencyProperty ActionProperty = DependencyProperty.RegisterAttached(
        "Action", typeof(string), typeof(ShortcutToolTip), new PropertyMetadata(string.Empty, OnActionChanged));

    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(ShortcutToolTip), new PropertyMetadata(string.Empty, OnTextChanged));

    private static readonly ConditionalWeakTable<FrameworkElement, State> sStates = [];

    public static string GetAction(DependencyObject obj)
    {
        return obj.GetValue(ActionProperty) as string ?? string.Empty;
    }

    public static void SetAction(DependencyObject obj, string value)
    {
        obj.SetValue(ActionProperty, value);
    }

    public static string GetText(DependencyObject obj)
    {
        return obj.GetValue(TextProperty) as string ?? string.Empty;
    }

    public static void SetText(DependencyObject obj, string value)
    {
        obj.SetValue(TextProperty, value);
    }

    public static void Apply(FrameworkElement element, string action, string text)
    {
        SetText(element, text);
        SetAction(element, action);
    }

    private static void OnActionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        Update(d);
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        Update(d);
    }

    private static void Update(DependencyObject d)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        State state = sStates.GetValue(element, CreateState);
        state.Action = GetAction(element);
        state.Text = GetText(element);
        state.ToolTip.Content = Compose(state.Text, state.Action);

        // Recompute on every open so the tooltip reflects the latest shortcut configuration.
        ToolTipService.SetToolTip(element, state.ToolTip);
    }

    private static State CreateState(FrameworkElement element)
    {
        State state = new();
        state.ToolTip.Opened += (sender, e) =>
        {
            if (sender is ToolTip toolTip)
            {
                toolTip.Content = Compose(state.Text, state.Action);
            }
        };
        return state;
    }

    private static string Compose(string text, string action)
    {
        if (string.IsNullOrEmpty(action))
        {
            return text;
        }

        string shortcutText = KeyboardShortcutManager.GetShortcutDisplayText(action);
        if (string.IsNullOrEmpty(shortcutText))
        {
            return text;
        }

        return $"{text} ({shortcutText})";
    }

    private sealed class State
    {
        public string Action { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public ToolTip ToolTip { get; } = new();
    }
}
