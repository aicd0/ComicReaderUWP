// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Common.Lifecycle;

public interface ILiveDataValueAbility<T> where T : notnull
{
    public T? GetValue();
}
