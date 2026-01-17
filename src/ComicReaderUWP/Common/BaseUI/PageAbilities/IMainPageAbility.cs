// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Helpers.Navigation;
using ComicReaderUWP.SDK.Common.Lifecycle;
using ComicReaderUWP.Views.Pages.Main;

namespace ComicReaderUWP.Common.BaseUI.PageAbilities;

internal interface IMainPageAbility : IPageAbility
{
    public delegate void TabUnselectedEventHandler();
    public delegate void TitleBarVisibilityChangedEventHandler(bool visible);

    void OpenInCurrentTab(Route route);

    void OpenInNewTab(Route route);

    void RegisterTitleBarVisibilityChangedHandler(ILifecycleOwner owner, TitleBarVisibilityChangedEventHandler handler);

    void ShowOrHideTitleBar(bool show);

    bool GetSidePaneOpenState();

    void SetSidePaneOpenState(bool open, bool force);

    void SetSidePanePage(SidePaneView.PageEnum page);
}
