// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.BaseUI;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.Lifecycle;

namespace ComicReader.Views.Pages.Main;

internal interface IMainPageAbility : IPageAbility
{
    public delegate void TabUnselectedEventHandler();
    public delegate void TitleBarVisibilityChangedEventHandler(bool visible);

    void OpenInCurrentTab(Route route);

    void OpenInNewTab(Route route);

    void RegisterTabUnselectedHandler(ILifecycleOwner owner, TabUnselectedEventHandler handler);

    void RegisterTitleBarVisibilityChangedHandler(ILifecycleOwner owner, TitleBarVisibilityChangedEventHandler handler);

    void ShowOrHideTitleBar(bool show);

    bool GetSidePaneOpenState();

    void SetSidePaneOpenState(bool open, bool force);
}
