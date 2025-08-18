// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Common.Lifecycle;

public interface ILiveData<T> : ILiveDataNoType
{
    public void Observe(ILifecycleOwner owner, IObserver<T> observer);

    public void ObserveSticky(ILifecycleOwner owner, IObserver<T> observer);

    public T? GetValue();
}
