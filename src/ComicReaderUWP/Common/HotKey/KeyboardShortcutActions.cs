// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReaderUWP.Common.Localization;

using Windows.System;

namespace ComicReaderUWP.Common.HotKey;

internal static class KeyboardShortcutActions
{
    public const string ExitFullscreen = "ExitFullscreen";
    public const string GoToFirstPage = "GoToFirstPage";
    public const string GoToLastPage = "GoToLastPage";
    public const string GoToLeftPage = "GoToLeftPage";
    public const string GoToNextPage = "GoToNextPage";
    public const string GoToPreviousPage = "GoToPreviousPage";
    public const string GoToRandomPage = "GoToRandomPage";
    public const string GoToRightPage = "GoToRightPage";
    public const string OpenDevTools = "OpenDevTools";
    public const string ToggleAutoScroll = "ToggleAutoScroll";
    public const string ToggleFullscreen = "ToggleFullscreen";

    public const string RootScope = "";
    public const string ReaderScope = "/Reader";

    private static readonly List<KeyboardShortcutActionEntry> sAll =
    [
        new() { Id = ExitFullscreen, Name = StringResourceProvider.Instance.ExitFullscreen, IsInternal = true },
        new() { Id = GoToFirstPage, Name = StringResourceProvider.Instance.GoToFirstPage, Scope = ReaderScope },
        new() { Id = GoToLastPage, Name = StringResourceProvider.Instance.GoToLastPage, Scope = ReaderScope },
        new() { Id = GoToLeftPage, Name = StringResourceProvider.Instance.GoToLeftPage, Scope = ReaderScope },
        new() { Id = GoToNextPage, Name = StringResourceProvider.Instance.GoToNextPage, Scope = ReaderScope },
        new() { Id = GoToPreviousPage, Name = StringResourceProvider.Instance.GoToPreviousPage, Scope = ReaderScope },
        new() { Id = GoToRandomPage, Name = StringResourceProvider.Instance.GoToRandomPage, Scope = ReaderScope },
        new() { Id = GoToRightPage, Name = StringResourceProvider.Instance.GoToRightPage, Scope = ReaderScope },
        new() { Id = OpenDevTools, Name = "Open dev tools", IsInternal = true },
        new() { Id = ToggleAutoScroll, Name = StringResourceProvider.Instance.ToggleAutoScroll, Scope = ReaderScope },
        new() { Id = ToggleFullscreen, Name = StringResourceProvider.Instance.ToggleFullscreen },
    ];

    private static readonly IReadOnlyList<KeyboardShortcutModel> sDefaultShortcuts =
    [
        CreateShortcut(GoToFirstPage, VirtualKey.Home),
        CreateShortcut(GoToLastPage, VirtualKey.End),
        CreateShortcut(GoToLeftPage, VirtualKey.Left),
        CreateShortcut(GoToNextPage, VirtualKey.Down),
        CreateShortcut(GoToNextPage, VirtualKey.PageDown),
        CreateShortcut(GoToPreviousPage, VirtualKey.PageUp),
        CreateShortcut(GoToPreviousPage, VirtualKey.Up),
        CreateShortcut(GoToRandomPage, VirtualKey.R),
        CreateShortcut(GoToRightPage, VirtualKey.Right),
        CreateShortcut(ToggleAutoScroll, VirtualKey.Space),
    ];
    public static IReadOnlyList<KeyboardShortcutModel> DefaultShortcuts => sDefaultShortcuts;

    private static readonly IReadOnlyList<KeyboardShortcutModel> sInternalShortcuts =
    [
        CreateShortcut(ExitFullscreen, VirtualKey.Escape),
        CreateShortcut(OpenDevTools, VirtualKey.F11, VirtualKeyModifiers.Control),
    ];
    public static IReadOnlyList<KeyboardShortcutModel> InternalShortcuts => sInternalShortcuts;

    public static List<KeyboardShortcutActionEntry> GetAllEntries()
    {
        List<KeyboardShortcutActionEntry> entries = [];
        foreach (KeyboardShortcutActionEntry entry in sAll)
        {
            if (entry.IsInternal)
            {
                continue;
            }

            entries.Add(entry);
        }

        return entries;
    }

    public static KeyboardShortcutActionEntry? Get(string action)
    {
        foreach (KeyboardShortcutActionEntry entry in sAll)
        {
            if (entry.Id == action)
            {
                return entry;
            }
        }

        return null;
    }

    private static KeyboardShortcutModel CreateShortcut(string action, VirtualKey key, VirtualKeyModifiers modifiers = VirtualKeyModifiers.None)
    {
        return new KeyboardShortcutModel
        {
            Action = action,
            Key = key,
            Modifiers = modifiers,
        };
    }
}

internal sealed class KeyboardShortcutActionEntry
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string Scope { get; init; } = KeyboardShortcutActions.RootScope;

    public bool IsInternal { get; init; }
}
