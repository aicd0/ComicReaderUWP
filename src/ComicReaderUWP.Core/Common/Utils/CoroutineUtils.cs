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

    public static async Task Run(ITaskDispatcher dispatcher, Action action)
    {
        TaskCompletionSource<bool> completionSource = new();
        dispatcher.Submit(() =>
        {
            action();
            completionSource.SetResult(true);
        });

        await completionSource.Task;
    }

    public static async Task<T> Run<T>(ITaskDispatcher dispatcher, Func<T> function)
    {
        TaskCompletionSource<T> completionSource = new();
        dispatcher.Submit(() =>
        {
            completionSource.SetResult(function());
        });

        return await completionSource.Task;
    }

    public static async Task<T> RunAsyncTask<T>(ITaskDispatcher dispatcher, Func<Task<T>> function)
    {
        TaskCompletionSource<T> completionSource = new();
        dispatcher.Submit(() =>
        {
            Run(async () =>
            {
                completionSource.SetResult(await function());
            });
        });

        return await completionSource.Task;
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
