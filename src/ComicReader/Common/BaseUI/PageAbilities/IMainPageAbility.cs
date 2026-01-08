// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.Lifecycle;

namespace ComicReader.Common.BaseUI.PageAbilities;

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
}
