// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;

namespace ComicReaderUWP.Core.Common.Lifecycle;

public class EventBus : IEventBus
{
    public static readonly IEventBus Default = new EventBus();

    private readonly ConcurrentDictionary<string, ILiveDataCommonAbility> _topics = [];

    public IMutableLiveData<T> With<T>(string eventId) where T : notnull
    {
        while (true)
        {
            if (_topics.TryGetValue(eventId, out ILiveDataCommonAbility? topic))
            {
                return (IMutableLiveData<T>)topic;
            }

            var newTopic = new MutableLiveData<T>();
            if (_topics.TryAdd(eventId, newTopic))
            {
                return newTopic;
            }
        }
    }

    public IMutableLiveData<object> With(string eventId)
    {
        return With<object>(eventId);
    }

    public void Clear()
    {
        while (!_topics.IsEmpty)
        {
            List<string> keys = [.. _topics.Keys];
            foreach (string key in keys)
            {
                if (_topics.TryRemove(key, out ILiveDataCommonAbility? topic))
                {
                    topic.Clear();
                }
            }
        }
    }
}
