// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.Lifecycle;

namespace ComicReader.Common.BaseUI.PageAbilities;

internal interface IMainWindowAbility : IPageAbility
{
    public delegate void FullscreenChangedEventHandler(bool isFullscreen);

    int WindowId { get; }

    bool PointerInWindow();

    void EnterFullscreen();

    void ExitFullscreen();

    void RegisterFullscreenChangedHandler(ILifecycleOwner owner, FullscreenChangedEventHandler handler);
}
