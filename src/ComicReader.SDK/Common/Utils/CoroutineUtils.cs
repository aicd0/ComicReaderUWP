// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Threading;

using Microsoft.UI.Dispatching;

namespace ComicReader.SDK.Common.Utils;

public static class CoroutineUtils
{
    public static void Start(Func<Task> task)
    {
        task().ContinueWith(t =>
        {
            AggregateException? exception = t.Exception;
            if (exception is not null)
            {
                DebugUtils.CaptureFatalError(exception.Message, exception);
            }
        });
    }

    public static void RunInMainThread(Action action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        Start(() => MainThreadUtils.RunInMainThread(action, priority));
    }

    public static void PostInMainThread(Action action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        Start(() => MainThreadUtils.PostInMainThread(action, priority));
    }

    public static void RunInMainThreadAsync(Func<Task> action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        Start(() => MainThreadUtils.RunInMainThreadAsync(action, priority));
    }

    public static void PostInMainThreadAsync(Func<Task> action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        Start(() => MainThreadUtils.PostInMainThreadAsync(action, priority));
    }

    public static async Task<T> CreateTask<T>(string taskName, ITaskDispatcher dispatcher, Func<T> function)
    {
        TaskCompletionSource<T> completionSource = new();
        dispatcher.Submit(taskName, () =>
        {
            completionSource.SetResult(function());
        });

        return await completionSource.Task;
    }

    public static async Task<T> CreateTaskAsync<T>(string taskName, ITaskDispatcher dispatcher, Func<Task<T>> function)
    {
        TaskCompletionSource<T> completionSource = new();
        dispatcher.Submit(taskName, () =>
        {
            Start(async () =>
            {
                completionSource.SetResult(await function());
            });
        });

        return await completionSource.Task;
    }
}
