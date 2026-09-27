// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Common.Lifecycle;

public interface IEventBus
{
    public IMutableLiveData<T> With<T>(string eventId, Func<IMutableLiveData<T>> createFunc) where T : notnull;

    public void Clear();
}
