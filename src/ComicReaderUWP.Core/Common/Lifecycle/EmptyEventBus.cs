// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Common.Lifecycle;

public class EmptyEventBus : IEventBus
{
    public static readonly EmptyEventBus Instance = new();

    private EmptyEventBus() { }

    public IMutableLiveData<T> With<T>(string eventId, Func<IMutableLiveData<T>> createFunc) where T : notnull
    {
        return new EmptyLiveData<T>();
    }

    public void Clear()
    {
    }
}
