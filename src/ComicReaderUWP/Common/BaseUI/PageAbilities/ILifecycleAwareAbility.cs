// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.SDK.Common.Lifecycle;

namespace ComicReaderUWP.Common.BaseUI.PageAbilities;

public delegate void PageLifecycleEventHandler(ILifecycle.State state);

internal interface ILifecycleAwareAbility : IPageAbility
{
    void RegisterPageLifecycleHandler(PageLifecycleEventHandler handler);

    void UnregisterPageLifecycleHandler(PageLifecycleEventHandler handler);
}
