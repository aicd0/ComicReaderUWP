// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.Lifecycle;

public class MutableLiveData<T> : LiveData<T>, IMutableLiveData<T>
{
    public MutableLiveData() : base() { }

    public MutableLiveData(T initialValue) : base(initialValue) { }

    public void Emit(T value)
    {
        EmitInternal(value);
    }
}
