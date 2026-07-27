// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.Lifecycle;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReaderUWP.Common.BaseUI.PageAbilities;

internal interface IMainPageAbilityForTab : IMainPageAbility
{
    public delegate void CommonEventHandler();

    string TabId { get; }

    string Url { get; }

    void RegisterRefreshHandler(ILifecycleOwner owner, CommonEventHandler handler);

    void SetTitle(string title);

    void SetIcon(IconSource icon);

    void SetUrl(string url);

    void SetCustomNavigationBar(UIElement? element);

    void SetHiddenOverlayHitTestVisibility(bool visible);
}
