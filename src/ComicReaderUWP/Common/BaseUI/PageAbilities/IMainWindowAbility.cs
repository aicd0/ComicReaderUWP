// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Drawing;

using ComicReaderUWP.Common.Plugins;
using ComicReaderUWP.Core.Common.Lifecycle;

namespace ComicReaderUWP.Common.BaseUI.PageAbilities;

internal interface IMainWindowAbility : IPageAbility
{
    public delegate void MinimizeChangedEventHandler(bool isMinimized);

    public delegate void FullscreenChangedEventHandler(bool isFullscreen);

    public delegate void PointerInsideChangedEventHandler(bool isPointerInside);

    bool IsActive { get; }

    bool IsFullscreen { get; }

    bool IsMinimized { get; }

    PluginWindowContext PluginWindowContext { get; }

    int WindowId { get; }

    SizeF WindowSize { get; }

    bool IsFocusLocked { get; }

    void EnterFullscreen();

    void ExitFullscreen();

    PointF GetPointerPosition();

    double GetRasterizationScale();

    void RegisterFullscreenChangedHandler(ILifecycleOwner owner, FullscreenChangedEventHandler handler);

    void RegisterMinimizeChangedHandler(ILifecycleOwner owner, MinimizeChangedEventHandler handler);

    void RegisterPointerInsideRootElementChangedHandler(ILifecycleOwner owner, PointerInsideChangedEventHandler handler);

    void RequestFocusLock();

    void ReleaseFocusLock();

}
