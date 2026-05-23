// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Common.Lifecycle;

public interface ILiveData<T> : ILiveDataNoType where T : notnull
{
    public void Observe(ILifecycleOwner owner, IObserver<T> observer, ObserveOptions options);

    public T? GetValue();
}
