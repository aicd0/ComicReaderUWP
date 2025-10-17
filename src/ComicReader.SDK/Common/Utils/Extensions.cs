// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using ComicReader.SDK.Common.Lifecycle;

namespace ComicReader.SDK.Common.Utils;

public static class Extensions
{
    private static readonly ObserveOptions sObserveOptionDefault = new();
    private static readonly ObserveOptions sObserveOptionSticky = new()
    {
        StickyOnObserve = true,
    };

    public static void SafeAppend(this StringBuilder sb, string category, Func<object?> func)
    {
        string value;
        try
        {
            value = func()?.ToString() ?? "[null]";
        }
        catch (Exception)
        {
            return;
        }
        sb.Append(category);
        sb.Append(": ");
        sb.Append(value);
        sb.Append('\n');
    }

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

    private class Observer<U>(Action<U> action) : SDK.Common.Lifecycle.IObserver<U>
    {
        private readonly Action<U> _action = action;

        public void OnChanged(U value)
        {
            _action(value);
        }
    }
}
