// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReaderUWP.Common.Actions;
using ComicReaderUWP.Common.Actions.Components;
using ComicReaderUWP.Common.BaseUI;
using ComicReaderUWP.Common.BaseUI.PageAbilities;
using ComicReaderUWP.SDK.Plugins.UI;

namespace ComicReaderUWP.Common.Plugins;

internal sealed class UIContext : IUIContext
{
    private const string TAG = nameof(UIContext);

    //
    // Factory Methods
    //

    public static UIContext Create(PageCommunicator communicator)
    {
        IMainWindowAbility mainWindowAbility = communicator.GetAbility<IMainWindowAbility>() ?? throw new InvalidOperationException("IMainWindowAbility not found");
        int windowId = mainWindowAbility.WindowId;
        return new UIContext(windowId);
    }

    public static UIContext Create(ActionHandler actionHandler)
    {
        if (!actionHandler.TryGetComponent<IMainWindowComponent>(out IMainWindowComponent? mainWindowCom))
        {
            throw new InvalidOperationException("IMainWindowAbility not found");
        }

        int windowId = mainWindowCom.WindowId;
        return new UIContext(windowId);
    }

    //
    // Variables
    //

    private readonly int _windowId;

    private UIContext(int windowId)
    {
        _windowId = windowId;
    }

    //
    // IUIContext Implementation
    //

    public int WindowId => _windowId;
}
