// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.Lifecycle;

public class EventBus : IEventBus
{
    public static readonly IEventBus Default = new EventBus();

    private readonly Dictionary<string, ILiveDataNoType> _topics = [];
    private bool _clearing = false;

    public IMutableLiveData<T> With<T>(string eventId)
    {
        if (_clearing)
        {
            return new EmptyLiveData<T>();
        }

        if (_topics.TryGetValue(eventId, out ILiveDataNoType? topic))
        {
            return (IMutableLiveData<T>)topic;
        }

        var newTopic = new MutableLiveData<T>();
        _topics.Add(eventId, newTopic);
        return newTopic;
    }

    public IMutableLiveData<object> With(string eventId)
    {
        return With<object>(eventId);
    }

    public void Clear()
    {
        _clearing = true;
        foreach (ILiveDataNoType topic in _topics.Values)
        {
            topic.Clear();
        }
        _clearing = false;
        _topics.Clear();
    }
}
