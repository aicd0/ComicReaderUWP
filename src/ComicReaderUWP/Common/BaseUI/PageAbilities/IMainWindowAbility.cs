// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.Plugins;
using ComicReaderUWP.Core.Common.Lifecycle;

namespace ComicReaderUWP.Common.BaseUI.PageAbilities;

internal interface IMainWindowAbility : IPageAbility
{
    public delegate void MinimizeChangedEventHandler(bool isMinimized);

    public delegate void FullscreenChangedEventHandler(bool isFullscreen);

    public delegate void PointerOverWindowChangedEventHandler(bool isOver);

    int WindowId { get; }

    bool IsActive { get; }

    bool IsMinimized { get; }

    bool IsFullscreen { get; }

    PluginWindowContext PluginWindowContext { get; }

    void RegisterMinimizeChangedHandler(ILifecycleOwner owner, MinimizeChangedEventHandler handler);

    void RegisterFullscreenChangedHandler(ILifecycleOwner owner, FullscreenChangedEventHandler handler);

    void RegisterPointerOverWindowChangedEventHandler(ILifecycleOwner owner, PointerOverWindowChangedEventHandler handler);

    void EnterFullscreen();

    void ExitFullscreen();
}
