// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using Windows.System;

namespace ComicReaderUWP.Common.HotKey;

internal sealed class KeyboardShortcutModel
{
    public string Action { get; set; } = string.Empty;

    public VirtualKey Key { get; set; } = VirtualKey.None;

    public VirtualKeyModifiers Modifiers { get; set; } = VirtualKeyModifiers.None;

    public override string ToString()
    {
        string modifierText = FormatModifiers(Modifiers);
        string keyText = GetKeyName(Key);
        return string.IsNullOrEmpty(modifierText) ? keyText : $"{modifierText} + {keyText}";
    }

    public bool HasSameKeys(KeyboardShortcutModel other)
    {
        return Key == other.Key && Modifiers == other.Modifiers;
    }

    public static string FormatModifiers(VirtualKeyModifiers modifiers)
    {
        StringBuilder builder = new();

        if (modifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            builder.Append("Ctrl + ");
        }

        if (modifiers.HasFlag(VirtualKeyModifiers.Shift))
        {
            builder.Append("Shift + ");
        }

        if (modifiers.HasFlag(VirtualKeyModifiers.Menu))
        {
            builder.Append("Alt + ");
        }

        if (modifiers.HasFlag(VirtualKeyModifiers.Windows))
        {
            builder.Append("Win + ");
        }

        if (builder.Length > 0)
        {
            builder.Remove(builder.Length - 3, 3);
        }

        return builder.ToString();
    }

    private static string GetKeyName(VirtualKey key)
    {
        if (key >= VirtualKey.Number0 && key <= VirtualKey.Number9)
        {
            return ((int)key - (int)VirtualKey.Number0).ToString();
        }

        return key switch
        {
            VirtualKey.PageUp => "PgUp",
            VirtualKey.PageDown => "PgDn",
            VirtualKey.Insert => "Ins",
            VirtualKey.Delete => "Del",
            VirtualKey.Space => "Space",
            VirtualKey.Escape => "Esc",
            VirtualKey.Back => "Backspace",
            _ => key.ToString(),
        };
    }
}
