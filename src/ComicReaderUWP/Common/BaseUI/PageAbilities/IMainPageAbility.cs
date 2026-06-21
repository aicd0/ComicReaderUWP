// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Helpers.Navigation;

namespace ComicReaderUWP.Common.BaseUI.PageAbilities;

internal interface IMainPageAbility : IPageAbility
{
    public delegate void TabUnselectedEventHandler();
    public delegate void TitleBarVisibilityChangedEventHandler(bool visible);

    void RegisterOverlayVisibilityChangedHandler(ILifecycleOwner owner, TitleBarVisibilityChangedEventHandler handler);

    bool GetSidePaneOpenState();

    void SetOverlayVisibility(bool isVisible);

    void SetSidePaneOpenState(bool isOpen, bool force);

    void SetSidePanePage(string tag);

    void OpenInCurrentTab(Route route);

    void OpenInNewTab(Route route);
}
