// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.Lifecycle;

public interface ILiveData<T> : ILiveDataNoType
{
    public void Observe(ILifecycleOwner owner, IObserver<T> observer, ObserveOptions options);

    public T? GetValue();
}
