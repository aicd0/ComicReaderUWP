// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Common.Lifecycle;

public class EmptyLiveData<T> : IMutableLiveData<T> where T : notnull
{
    public void Clear()
    {
    }

    public void Emit(T value)
    {
    }

    public void Observe(ILifecycleOwner owner, IValueObserver<T> observer, ObserveOptions options)
    {
    }

    public void RemoveObserver(IValueObserver<T> observer)
    {
    }

    public bool HasObserver(IValueObserver<T> observer)
    {
        return false;
    }

    public T? GetValue()
    {
        return default;
    }
}
