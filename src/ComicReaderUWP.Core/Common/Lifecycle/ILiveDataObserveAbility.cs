// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Common.Lifecycle;

public interface ILiveDataObserveAbility<T> where T : class
{
    public void Observe(ILifecycleOwner owner, T observer, ObserveOptions options);

    public void RemoveObserver(T observer);

    public bool HasObserver(T observer);

}
