// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Database.KV;
using ComicReaderUWP.SDK.Models;

namespace ComicReaderUWP.Core.Common.Utils;

public static class Extensions
{
    //
    // Lifecycle
    //

    private static readonly ObserveOptions sObserveOptionDefault = new();

    private static readonly ObserveOptions sObserveOptionSticky = new()
    {
        Sticky = true,
    };

    public static void Observe<T>(this ILiveDataObserveAbility<T> liveData, ILifecycleOwner owner, T observer) where T : class
    {
        liveData.Observe(owner, observer, sObserveOptionDefault);
    }

    public static void Observe<T>(this ILiveDataObserveAbility<IValueObserver<T>> liveData, ILifecycleOwner owner, Action<T> observer) where T : notnull
    {
        var wrapper = new Observer<T>(observer);
        liveData.Observe(owner, wrapper);
    }

    public static void Observe<T>(this ILiveDataObserveAbility<IValueObserver<T>> liveData, ILifecycleOwner owner, Action<T> observer, ObserveOptions options) where T : notnull
    {
        var wrapper = new Observer<T>(observer);
        liveData.Observe(owner, wrapper, options);
    }

    public static void ObserveSticky<T>(this ILiveDataObserveAbility<T> liveData, ILifecycleOwner owner, T observer) where T : class
    {
        liveData.Observe(owner, observer, sObserveOptionSticky);
    }

    public static void ObserveSticky<T>(this ILiveDataObserveAbility<IValueObserver<T>> liveData, ILifecycleOwner owner, Action<T> observer) where T : notnull
    {
        var wrapper = new Observer<T>(observer);
        liveData.ObserveSticky(owner, wrapper);
    }

    private class Observer<U>(Action<U> action) : Lifecycle.IValueObserver<U>
    {
        private readonly Action<U> _action = action;

        public void OnChanged(U value)
        {
            _action(value);
        }
    }

    //
    // Registry
    //

    public static T? GetValue<T>(this IRegistryKey registry, string key)
    {
        if (registry.TryGet(key, out T? value))
        {
            return value;
        }

        return default;
    }

    public static T GetValueOrDefault<T>(this IRegistryKey registry, string key, T defaultValue)
    {
        if (registry.TryGet(key, out T? value) && value is not null)
        {
            return value;
        }

        return defaultValue;
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
