// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Threading;

using Microsoft.UI.Dispatching;

namespace ComicReaderUWP.Core.Common.Utils;

public static class CoroutineUtils
{
    public static void Run(Func<Task> task)
    {
        task().ContinueWith(t =>
        {
            AggregateException? ex = t.Exception;
            if (ex is not null)
            {
                DebugUtils.CaptureFatalError("An unknown error occurred in CoroutineUtils#Run.", ex);
            }
        });
    }

    public static void RunInMainThread(Action action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        Run(() => MainThreadUtils.RunInMainThread(action, priority));
    }

    public static void PostInMainThread(Action action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        Run(() => MainThreadUtils.PostInMainThread(action, priority));
    }

    public static void RunInMainThreadAsync(Func<Task> action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        Run(() => MainThreadUtils.RunInMainThreadAsync(action, priority));
    }

    public static void PostInMainThreadAsync(Func<Task> action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        Run(() => MainThreadUtils.PostInMainThreadAsync(action, priority));
    }
}
