// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.Lifecycle;

public interface IEventBus
{
    public IMutableLiveData<T> With<T>(string eventId);

    public IMutableLiveData<object> With(string eventId);

    public void Clear();
}
