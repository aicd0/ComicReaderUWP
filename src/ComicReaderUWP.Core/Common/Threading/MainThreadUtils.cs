// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.Test;

using Microsoft.UI.Dispatching;

namespace ComicReaderUWP.Core.Common.Threading;

public static class MainThreadUtils
{
    private static DispatcherQueue? _mainDispatcherQueue = null;

    public static void Initialize(DispatcherQueue dispatcherQueue)
    {
        _mainDispatcherQueue = dispatcherQueue;
    }

    public static void AssertOnMainThread()
    {
        if (!IsMainThread())
        {
            throw new InvalidOperationException("This operation must be performed on the main thread.");
        }
    }

    public static DispatcherQueueTimer CreateTimer()
    {
        DispatcherQueue dispatcher = GetMainThreadDispatcher() ?? throw new InvalidOperationException("Main thread dispatcher is currently unavailable");
        return dispatcher.CreateTimer();
    }

    public static async Task RunInMainThread(Action action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        await RunInMainThread(action, priority, true);
    }

    public static async Task PostInMainThread(Action action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        await RunInMainThread(action, priority, false);
    }

    public static async Task RunInMainThreadAsync(Func<Task> action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        await RunInMainThreadAsync(action, priority, true);
    }

    public static async Task PostInMainThreadAsync(Func<Task> action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        await RunInMainThreadAsync(action, priority, false);
    }

    private static async Task RunInMainThread(Action action, DispatcherQueuePriority priority, bool runImmediatelyIfPossible)
    {
        if (TestSettings.UseCurrentThreadAsMainThread)
        {
            action();
            return;
        }

        DispatcherQueue dispatcher = GetMainThreadDispatcher() ?? throw new InvalidOperationException("Main thread dispatcher is currently unavailable");
        if (runImmediatelyIfPossible && dispatcher.HasThreadAccess)
        {
            action();
            return;
        }

        var taskCompletionSource = new TaskCompletionSource<bool>();
        bool success = dispatcher.TryEnqueue(priority, delegate
        {
            try
            {
                action();
                taskCompletionSource.SetResult(true);
            }
            catch (Exception e)
            {
                taskCompletionSource.SetException(e);
            }
        });

        if (!success)
        {
            throw new InvalidOperationException("Failed to enqueue the operation");
        }

        await taskCompletionSource.Task;
    }

    private static async Task RunInMainThreadAsync(Func<Task> action, DispatcherQueuePriority priority, bool runImmediatelyIfPossible)
    {
        if (TestSettings.UseCurrentThreadAsMainThread)
        {
            await action();
            return;
        }

        DispatcherQueue dispatcher = GetMainThreadDispatcher() ?? throw new InvalidOperationException("Main thread dispatcher is currently unavailable");
        if (runImmediatelyIfPossible && dispatcher.HasThreadAccess)
        {
            await action();
            return;
        }

        var taskCompletionSource = new TaskCompletionSource<bool>();
        bool success = dispatcher.TryEnqueue(priority, async delegate
        {
            try
            {
                await action();
                taskCompletionSource.SetResult(true);
            }
            catch (Exception e)
            {
                taskCompletionSource.SetException(e);
            }
        });

        if (!success)
        {
            throw new InvalidOperationException("Failed to enqueue the operation");
        }

        await taskCompletionSource.Task;
    }

    public static bool IsMainThread()
    {
        if (TestSettings.UseCurrentThreadAsMainThread)
        {
            return true;
        }

        DispatcherQueue? queue = GetMainThreadDispatcher();
        if (queue == null)
        {
            return false;
        }

        return queue.HasThreadAccess;
    }

    private static DispatcherQueue? GetMainThreadDispatcher()
    {
        return _mainDispatcherQueue;
    }
}
