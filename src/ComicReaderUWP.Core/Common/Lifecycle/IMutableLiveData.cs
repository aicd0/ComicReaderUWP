// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Common.Lifecycle;

public interface IMutableLiveData<T> : ILiveData<T>, ILiveDataEmitAbility<T> where T : notnull
{
}
