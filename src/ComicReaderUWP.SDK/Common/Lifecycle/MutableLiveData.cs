// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Common.Lifecycle;

public class MutableLiveData<T> : LiveData<T>, IMutableLiveData<T> where T : notnull
{
    public MutableLiveData() : base() { }

    public MutableLiveData(T initialValue) : base(initialValue) { }

    public void Emit(T value)
    {
        EmitInternal(value);
    }
}
