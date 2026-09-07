// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Actions.Components;
using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.BaseUI.PageAbilities;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.SDK.Models;
using ComicReaderUWP.SDK.Plugins.Comic;
using ComicReaderUWP.SDK.Plugins.UI;

using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Common.Plugins;

internal sealed class PluginWindowContext(int windowId) : IWindowContext
{
    //
    // Factory
    //

    public static PluginWindowContext From(PageCommunicator communicator)
    {
        return communicator.GetAbility<IMainWindowAbility>()!.PluginWindowContext;
    }

    public static PluginWindowContext From(ActionHandler actionHandler)
    {
        if (!actionHandler.TryGetComponent(out IMainWindowComponent? component))
        {
            throw new ArgumentException("IMainWindowComponent not found.");
        }

        return component.PluginWindowContext;
    }

    //
    // Variables
    //

    private readonly int _windowId = windowId;

    //
    // Public API
    //

    public void SetReadingComic(IComicModel? comic, Func<Action?, bool> executor)
    {
        if (comic == _readingComic)
        {
            return;
        }

        _readingComic = comic;
        executor(() => _readingComicChanged?.Invoke(comic));
    }

    //
    // IWindowContext Implementation
    //

    private event ReadingComicChangedEventHandler? _readingComicChanged;

    private IComicModel? _readingComic;

    event ReadingComicChangedEventHandler? IWindowContext.ReadingComicChanged
    {
        add { _readingComicChanged += value; }
        remove { _readingComicChanged -= value; }
    }

    public IComicModel? ReadingComic => _readingComic;

    Task<DialogResult> IWindowContext.EnqueueDialog(DialogOptions options)
    {
        return DialogUtils.EnqueueDialogAsync(_windowId, options);
    }

    Task<DialogResult> IWindowContext.EnqueueDialog(ContentDialog dialog)
    {
        return DialogUtils.EnqueueDialogAsync(_windowId, dialog);
    }
}
