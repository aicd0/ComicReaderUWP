// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReaderUWP.Core.Common.Lifecycle;

public class EventBus : IEventBus
{
    public static readonly IEventBus Default = new EventBus();

    private readonly Dictionary<string, ILiveDataCommonAbility> _topics = [];
    private bool _clearing = false;

    public IMutableLiveData<T> With<T>(string eventId) where T : notnull
    {
        if (_clearing)
        {
            return new EmptyLiveData<T>();
        }

        if (_topics.TryGetValue(eventId, out ILiveDataCommonAbility? topic))
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
        try
        {
            foreach (ILiveDataCommonAbility topic in _topics.Values)
            {
                topic.Clear();
            }

            _topics.Clear();
        }
        finally
        {
            _clearing = false;
        }
    }
}
