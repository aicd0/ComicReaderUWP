// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.BaseUI;
using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.Lifecycle;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Pages.Main;

internal interface IMainPageAbility : IPageAbility
{
    public delegate void TabUnselectedEventHandler();
    public delegate void TitleBarVisibilityChangedEventHandler(bool visible);

    void OpenInCurrentTab(Route route);

    void OpenInNewTab(Route route);

    void SetTitle(string title);

    void SetIcon(IconSource icon);

    void RegisterTabUnselectedHandler(ILifecycleOwner owner, TabUnselectedEventHandler handler);

    void RegisterTitleBarVisibilityChangedHandler(ILifecycleOwner owner, TitleBarVisibilityChangedEventHandler handler);

    void ShowOrHideTitleBar(bool show);

    void SetSidePaneOpen(bool open, bool force);
}
