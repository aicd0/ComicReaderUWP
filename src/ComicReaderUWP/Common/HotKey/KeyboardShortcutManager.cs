// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Lifecycle.Utils;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Misc;

using Microsoft.UI.Xaml.Input;

using Windows.Foundation;
using Windows.System;

namespace ComicReaderUWP.Common.HotKey;

internal static class KeyboardShortcutManager
{
    private const long TRIGGER_COOLDOWN = 200;

    private static readonly MutableLiveData<bool> sShortcutsChangedLiveData = new();
    public static ILiveData<bool> ShortcutsChangedLiveData => sShortcutsChangedLiveData;

    private static readonly Dictionary<string, long> sLastTriggerTime = [];
    private static List<KeyboardShortcutModel>? sCachedShortcuts;

    static KeyboardShortcutManager()
    {
        AppSettingsModel.KeyboardShortcutsChangedLiveData.Observe(AlwaysActiveLifecycleOwner.Instance, _ =>
        {
            sCachedShortcuts = null;
            sShortcutsChangedLiveData.Emit(true);
        });
    }

    public static IReadOnlyList<KeyboardShortcutModel> GetShortcuts()
    {
        return GetCachedShortcuts();
    }

    public static void Add(KeyboardShortcutModel shortcut)
    {
        List<KeyboardShortcutModel> shortcuts = [.. GetCachedShortcuts()];
        shortcuts.Add(shortcut);
        AppSettingsModel.KeyboardShortcuts = shortcuts;
    }

    public static void Update(KeyboardShortcutModel original, KeyboardShortcutModel updated)
    {
        List<KeyboardShortcutModel> shortcuts = [.. GetCachedShortcuts()];
        for (int i = 0; i < shortcuts.Count; i++)
        {
            if (shortcuts[i].Action == original.Action && shortcuts[i].HasSameKeys(original))
            {
                shortcuts[i] = updated;
                break;
            }
        }

        AppSettingsModel.KeyboardShortcuts = shortcuts;
    }

    public static void Remove(KeyboardShortcutModel shortcut)
    {
        List<KeyboardShortcutModel> shortcuts = [.. GetCachedShortcuts()];
        for (int i = shortcuts.Count - 1; i >= 0; i--)
        {
            if (shortcuts[i].Action == shortcut.Action && shortcuts[i].HasSameKeys(shortcut))
            {
                shortcuts.RemoveAt(i);
                break;
            }
        }

        AppSettingsModel.KeyboardShortcuts = shortcuts;
    }

    public static List<KeyboardAccelerator> CreateAccelerators(string action, TypedEventHandler<KeyboardAccelerator, KeyboardAcceleratorInvokedEventArgs> handler)
    {
        List<KeyboardAccelerator> accelerators = [];
        foreach (KeyboardShortcutModel shortcut in GetShortcuts(action))
        {
            KeyboardAccelerator accelerator = new()
            {
                Key = shortcut.Key,
                Modifiers = shortcut.Modifiers,
            };
            accelerator.Invoked += (sender, args) =>
            {
                args.Handled = true;
                if (IsTriggerAllowed(shortcut))
                {
                    handler(sender, args);
                }
            };
            accelerators.Add(accelerator);
        }

        return accelerators;
    }

    public static bool IsReserved(VirtualKey key, VirtualKeyModifiers modifiers)
    {
        foreach (KeyboardShortcutModel shortcut in KeyboardShortcutActions.InternalShortcuts)
        {
            if (shortcut.Key == key && shortcut.Modifiers == modifiers)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsDuplicate(KeyboardShortcutModel candidate, KeyboardShortcutModel? original)
    {
        string candidateScope = GetScope(candidate.Action);
        foreach (KeyboardShortcutModel shortcut in GetCachedShortcuts())
        {
            if (original is not null && shortcut.Action == original.Action && shortcut.HasSameKeys(original))
            {
                continue;
            }

            if (!shortcut.HasSameKeys(candidate))
            {
                continue;
            }

            if (AreScopesRelated(candidateScope, GetScope(shortcut.Action)))
            {
                return true;
            }
        }

        return false;
    }

    private static List<KeyboardShortcutModel> GetShortcuts(string action)
    {
        List<KeyboardShortcutModel> shortcuts = [];
        foreach (KeyboardShortcutModel shortcut in GetCachedShortcuts())
        {
            if (shortcut.Action == action)
            {
                shortcuts.Add(shortcut);
            }
        }
        foreach (KeyboardShortcutModel shortcut in KeyboardShortcutActions.InternalShortcuts)
        {
            if (shortcut.Action == action)
            {
                shortcuts.Add(shortcut);
            }
        }
        return shortcuts;
    }

    private static List<KeyboardShortcutModel> GetCachedShortcuts()
    {
        return sCachedShortcuts ??= AppSettingsModel.KeyboardShortcuts;
    }

    private static bool IsTriggerAllowed(KeyboardShortcutModel shortcut)
    {
        string triggerKey = $"{shortcut.Action}:{shortcut.Key}:{shortcut.Modifiers}";
        long now = Environment.TickCount64;
        if (sLastTriggerTime.TryGetValue(triggerKey, out long lastTime))
        {
            long elapsedMilliseconds = now - lastTime;
            if (elapsedMilliseconds < TRIGGER_COOLDOWN)
            {
                return false;
            }
        }

        sLastTriggerTime[triggerKey] = now;
        return true;
    }

    private static string GetScope(string action)
    {
        return KeyboardShortcutActions.Get(action)?.Scope ?? KeyboardShortcutActions.RootScope;
    }

    private static bool AreScopesRelated(string scopeA, string scopeB)
    {
        return scopeA == scopeB || scopeA.StartsWith(scopeB + "/", StringComparison.Ordinal) || scopeB.StartsWith(scopeA + "/", StringComparison.Ordinal);
    }
}
