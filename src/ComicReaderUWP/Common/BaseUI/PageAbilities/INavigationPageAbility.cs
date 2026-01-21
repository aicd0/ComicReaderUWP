// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.SDK.Common.Lifecycle;

using Microsoft.UI.Xaml;

namespace ComicReaderUWP.Common.BaseUI.PageAbilities;

internal interface INavigationPageAbility : IPageAbility
{
    public delegate void CommonEventHandler();

    void SetCustomNavigationBar(UIElement? element);

    void SetFullscreenButtonVisible(bool visible);

    void RegisterRefreshHandler(ILifecycleOwner owner, CommonEventHandler handler);
}
