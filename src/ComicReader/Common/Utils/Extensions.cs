// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;

using ComicReader.Common.Legacy;
using ComicReader.Common.Lifecycle;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace ComicReader.Common.Utils;

public static class Extensions
{
    public static bool Successful(this TaskException r)
    {
        return r == TaskException.Success;
    }

    public static IEnumerable<DependencyObject> ChildrenBreadthFirst(this DependencyObject obj, bool includeSelf = false)
    {
        if (includeSelf)
        {
            yield return obj;
        }

        var queue = new Queue<DependencyObject>();
        queue.Enqueue(obj);

        while (queue.Count > 0)
        {
            obj = queue.Dequeue();
            int count = VisualTreeHelper.GetChildrenCount(obj);

            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                yield return child;
                queue.Enqueue(child);
            }
        }
    }

    public static T Get<T>(this WeakReference<T> r) where T : class
    {
        if (r.TryGetTarget(out T obj))
        {
            return obj;
        }

        return null;
    }

    public static bool IsStarted(this ILifecycle.State state)
    {
        return state == ILifecycle.State.Started || state == ILifecycle.State.Resumed;
    }

    public static void Observe<T>(this ILiveData<T> liveData, ILifecycleOwner owner, Action<T> observer)
    {
        var wrapper = new Observer<T>(observer);
        liveData.Observe(owner, wrapper);
    }

    public static void ObserveSticky<T>(this ILiveData<T> liveData, ILifecycleOwner owner, Action<T> observer)
    {
        var wrapper = new Observer<T>(observer);
        liveData.ObserveSticky(owner, wrapper);
    }

    private class Observer<U>(Action<U> action) : Lifecycle.IObserver<U>
    {
        private readonly Action<U> _action = action;

        public void OnChanged(U value)
        {
            _action(value);
        }
    }
}
