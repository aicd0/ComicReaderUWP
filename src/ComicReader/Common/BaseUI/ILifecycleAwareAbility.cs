// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.Lifecycle;

namespace ComicReader.Common.BaseUI;

public delegate void PageLifecycleEventHandler(ILifecycle.State state);

internal interface ILifecycleAwareAbility : IPageAbility
{
    void RegisterPageLifecycleHandler(PageLifecycleEventHandler handler);

    void UnregisterPageLifecycleHandler(PageLifecycleEventHandler handler);
}
