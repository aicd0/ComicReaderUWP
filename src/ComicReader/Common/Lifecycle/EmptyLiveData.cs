// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;

namespace ComicReader.Common.Lifecycle;

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

    public void Observe(FrameworkElement owner, IObserver<T> observer)
    {
    }

    public void ObserveSticky(FrameworkElement owner, IObserver<T> observer)
    {
    }
}
