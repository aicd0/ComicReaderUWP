// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.SDK.Common.Lifecycle;
using ComicReaderUWP.SDK.Database.KV;

namespace ComicReaderUWP.SDK.Common.Utils;

public static class Extensions
{
    //
    // Lifecycle
    //

    private static readonly ObserveOptions sObserveOptionDefault = new();

    private static readonly ObserveOptions sObserveOptionSticky = new()
    {
        StickyOnObserve = true,
    };

    private static readonly ObserveOptions sObserveOptionStartSticky = new()
    {
        StickyOnObserve = true,
        ActiveOnStart = true,
    };

    public static bool IsStarted(this ILifecycle.State state)
    {
        return state == ILifecycle.State.Started || state == ILifecycle.State.Resumed;
    }

    public static void Observe<T>(this ILiveData<T> liveData, ILifecycleOwner owner, Action<T> observer)
    {
        var wrapper = new Observer<T>(observer);
        liveData.Observe(owner, wrapper, sObserveOptionDefault);
    }

    public static void ObserveSticky<T>(this ILiveData<T> liveData, ILifecycleOwner owner, Action<T> observer)
    {
        var wrapper = new Observer<T>(observer);
        liveData.Observe(owner, wrapper, sObserveOptionSticky);
    }

    public static void ObserveStartSticky<T>(this ILiveData<T> liveData, ILifecycleOwner owner, Action<T> observer)
    {
        var wrapper = new Observer<T>(observer);
        liveData.Observe(owner, wrapper, sObserveOptionStartSticky);
    }

    private class Observer<U>(Action<U> action) : SDK.Common.Lifecycle.IObserver<U>
    {
        private readonly Action<U> _action = action;

        public void OnChanged(U value)
        {
            _action(value);
        }
    }

    //
    // KV
    //

    public static T? GetValue<T>(this IKVCollection collection, string key)
    {
        if (collection.TryGet(key, out T? value))
        {
            return value;
        }

        return default;
    }

    public static T GetValueOrDefault<T>(this IKVCollection collection, string key, T defaultValue)
    {
        if (collection.TryGet(key, out T? value) && value is not null)
        {
            return value;
        }

        return defaultValue;
    }
}
