// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.SDK.Common.Lifecycle;

public interface IMutableLiveData<T> : ILiveData<T> where T : notnull
{
    void Emit(T value);
}
