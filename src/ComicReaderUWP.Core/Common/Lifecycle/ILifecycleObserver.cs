// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Common.Lifecycle;

public interface ILifecycleObserver
{
    void OnLifecycleEvent(ILifecycle.State fromState, ILifecycle.State toState);
}
