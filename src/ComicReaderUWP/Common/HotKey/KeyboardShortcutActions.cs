// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReaderUWP.Common.Localization;

using Windows.System;

namespace ComicReaderUWP.Common.HotKey;

internal static class KeyboardShortcutActions
{
    public const string AddNewTab = "AddNewTab";
    public const string CloseTab = "CloseTab";
    public const string ExitFullscreen = "ExitFullscreen";
    public const string GoToFirstPage = "GoToFirstPage";
    public const string GoToLastPage = "GoToLastPage";
    public const string GoToLeftPage = "GoToLeftPage";
    public const string GoToNextPage = "GoToNextPage";
    public const string GoToPreviousPage = "GoToPreviousPage";
    public const string GoToRandomPage = "GoToRandomPage";
    public const string GoToRightPage = "GoToRightPage";
    public const string JumpToNextTab = "JumpToNextTab";
    public const string JumpToPreviousTab = "JumpToPreviousTab";
    public const string OpenDevTools = "OpenDevTools";
    public const string OpenNextComic = "OpenNextComic";
    public const string OpenPreviousComic = "OpenPreviousComic";
    public const string ToggleAutoScroll = "ToggleAutoScroll";
    public const string ToggleFullscreen = "ToggleFullscreen";

    public const string RootScope = "";
    public const string ReaderScope = "/Reader";

    private static readonly List<KeyboardShortcutActionEntry> sAll =
    [
        new() { Id = AddNewTab, Name = StringResourceProvider.Instance.AddNewTab },
        new() { Id = CloseTab, Name = StringResourceProvider.Instance.CloseTab },
        new() { Id = ExitFullscreen, Name = StringResourceProvider.Instance.ExitFullscreen, IsInternal = true },
        new() { Id = GoToFirstPage, Name = StringResourceProvider.Instance.GoToFirstPage, Scope = ReaderScope },
        new() { Id = GoToLastPage, Name = StringResourceProvider.Instance.GoToLastPage, Scope = ReaderScope },
        new() { Id = GoToLeftPage, Name = StringResourceProvider.Instance.GoToLeftPage, Scope = ReaderScope },
        new() { Id = GoToNextPage, Name = StringResourceProvider.Instance.GoToNextPage, Scope = ReaderScope },
        new() { Id = GoToPreviousPage, Name = StringResourceProvider.Instance.GoToPreviousPage, Scope = ReaderScope },
        new() { Id = GoToRandomPage, Name = StringResourceProvider.Instance.GoToRandomPage, Scope = ReaderScope },
        new() { Id = GoToRightPage, Name = StringResourceProvider.Instance.GoToRightPage, Scope = ReaderScope },
        new() { Id = JumpToNextTab, Name = StringResourceProvider.Instance.JumpToNextTab },
        new() { Id = JumpToPreviousTab, Name = StringResourceProvider.Instance.JumpToPreviousTab },
        new() { Id = OpenDevTools, Name = "Open dev tools", IsInternal = true },
        new() { Id = OpenNextComic, Name = StringResourceProvider.Instance.OpenNextComic, Scope = ReaderScope },
        new() { Id = OpenPreviousComic, Name = StringResourceProvider.Instance.OpenPreviousComic, Scope = ReaderScope },
        new() { Id = ToggleAutoScroll, Name = StringResourceProvider.Instance.ToggleAutoScroll, Scope = ReaderScope },
        new() { Id = ToggleFullscreen, Name = StringResourceProvider.Instance.ToggleFullscreen },
    ];

    private static readonly IReadOnlyList<KeyboardShortcutModel> sDefaultShortcuts =
    [
        CreateShortcut(AddNewTab, VirtualKey.T, VirtualKeyModifiers.Control),
        CreateShortcut(CloseTab, VirtualKey.W, VirtualKeyModifiers.Control),
        CreateShortcut(GoToFirstPage, VirtualKey.Home),
        CreateShortcut(GoToLastPage, VirtualKey.End),
        CreateShortcut(GoToLeftPage, VirtualKey.Left),
        CreateShortcut(GoToNextPage, VirtualKey.Down),
        CreateShortcut(GoToNextPage, VirtualKey.PageDown),
        CreateShortcut(GoToPreviousPage, VirtualKey.PageUp),
        CreateShortcut(GoToPreviousPage, VirtualKey.Up),
        CreateShortcut(GoToRandomPage, VirtualKey.R),
        CreateShortcut(GoToRightPage, VirtualKey.Right),
        CreateShortcut(JumpToNextTab, VirtualKey.Tab, VirtualKeyModifiers.Control),
        CreateShortcut(JumpToPreviousTab, VirtualKey.Tab, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift),
        CreateShortcut(OpenNextComic, VirtualKey.Down, VirtualKeyModifiers.Control),
        CreateShortcut(OpenPreviousComic, VirtualKey.Up, VirtualKeyModifiers.Control),
        CreateShortcut(ToggleAutoScroll, VirtualKey.Space),
        CreateShortcut(ToggleFullscreen, VirtualKey.F, VirtualKeyModifiers.Control),
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
