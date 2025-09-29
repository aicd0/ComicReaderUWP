// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.Lifecycle;

public class EmptyLiveData<T> : IMutableLiveData<T>
{
    public void Clear()
    {
    }

    public void Emit(T value)
    {
    }

    public T? GetValue()
    {
        return default;
    }

    public void Observe(ILifecycleOwner owner, IObserver<T> observer)
    {
    }

    public void ObserveSticky(ILifecycleOwner owner, IObserver<T> observer)
    {
    }
}
