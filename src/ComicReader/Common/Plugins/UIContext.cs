// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.Actions;
using ComicReader.Common.Actions.Components;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Plugins.Common;

namespace ComicReader.Common.Plugins;

internal sealed class UIContext : IUIContext
{
    private const string TAG = nameof(UIContext);

    //
    // Factory Methods
    //

    public static UIContext Create(int windowId)
    {
        return new UIContext(windowId);
    }

    public static UIContext? Create(ActionHandler actionHandler)
    {
        if (!actionHandler.TryGetComponent<IMainWindowComponent>(out IMainWindowComponent? mainWindowCom))
        {
            Logger.F(TAG, "IMainWindowComponent component not found");
            return null;
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
