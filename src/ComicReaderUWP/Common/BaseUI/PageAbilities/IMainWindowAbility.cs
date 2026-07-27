// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.Plugins;
using ComicReaderUWP.Core.Common.Lifecycle;

namespace ComicReaderUWP.Common.BaseUI.PageAbilities;

internal interface IMainWindowAbility : IPageAbility
{
    public delegate void MinimizeChangedEventHandler(bool isMinimized);

    public delegate void FullscreenChangedEventHandler(bool isFullscreen);

    public delegate void PointerInsideChangedEventHandler(bool isPointerInside);

    int WindowId { get; }

    bool IsActive { get; }

    bool IsMinimized { get; }

    bool IsFullscreen { get; }

    PluginWindowContext PluginWindowContext { get; }

    bool GetPointerInsideWindowState();

    void RegisterMinimizeChangedHandler(ILifecycleOwner owner, MinimizeChangedEventHandler handler);

    void RegisterFullscreenChangedHandler(ILifecycleOwner owner, FullscreenChangedEventHandler handler);

    void RegisterPointerInsideRootElementChangedHandler(ILifecycleOwner owner, PointerInsideChangedEventHandler handler);

    void EnterFullscreen();

    void ExitFullscreen();
}
