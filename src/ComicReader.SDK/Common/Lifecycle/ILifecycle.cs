// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.Lifecycle;

public interface ILifecycle
{
    State GetState();

    void AddObserver(ILifecycleObserver observer);

    void RemoveObserver(ILifecycleObserver observer);

    public enum State
    {
        Initialized,
        Started,
        Resumed,
        Stopped,
    }
}
