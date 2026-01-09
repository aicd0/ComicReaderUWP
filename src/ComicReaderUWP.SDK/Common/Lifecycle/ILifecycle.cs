// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Common.Lifecycle;

public interface ILifecycle
{
    State GetState();

    void AddObserver(ILifecycleObserver observer);

    void RemoveObserver(ILifecycleObserver observer);

    public enum State
    {
        Initialized = 0,
        Stopped = 1,
        Started = 2,
        Resumed = 3,
    }
}
