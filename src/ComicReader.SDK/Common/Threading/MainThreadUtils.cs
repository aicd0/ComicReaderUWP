// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.Test;

using Microsoft.UI.Dispatching;

namespace ComicReader.SDK.Common.Threading;

public static class MainThreadUtils
{
    private static DispatcherQueue? _mainDispatcherQueue = null;

    public static void Initialize(DispatcherQueue dispatcherQueue)
    {
        _mainDispatcherQueue = dispatcherQueue;
    }

    public static Task RunInMainThread(Action action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        return RunInMainThread(action, priority, true);
    }

    public static Task PostInMainThread(Action action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        return RunInMainThread(action, priority, false);
    }

    public static Task RunInMainThreadAsync(Func<Task> action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        return RunInMainThreadAsync(action, priority, true);
    }

    public static Task PostInMainThreadAsync(Func<Task> action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
    {
        return RunInMainThreadAsync(action, priority, false);
    }

    private static Task RunInMainThread(Action action, DispatcherQueuePriority priority, bool runImmediatelyIfPossible)
    {
        if (TestSettings.UseCurrentThreadAsMainThread)
        {
            action();
            return Task.CompletedTask;
        }

        DispatcherQueue? dispatcher = GetMainThreadDispatcher();
        if (dispatcher is null)
        {
            return Task.FromException(new InvalidOperationException("Main thread dispatcher is currently unavailable"));
        }

        if (runImmediatelyIfPossible && dispatcher.HasThreadAccess)
        {
            try
            {
                action();
                return Task.CompletedTask;
            }
            catch (Exception e)
            {
                return Task.FromException(e);
            }
        }

        var taskCompletionSource = new TaskCompletionSource<object?>();
        bool success = dispatcher.TryEnqueue(priority, delegate
        {
            try
            {
                action();
                taskCompletionSource.SetResult(null);
            }
            catch (Exception e)
            {
                taskCompletionSource.SetException(e);
            }
        });

        if (!success)
        {
            taskCompletionSource.SetException(new InvalidOperationException("Failed to enqueue the operation"));
        }

        return taskCompletionSource.Task;
    }

    private static Task RunInMainThreadAsync(Func<Task> action, DispatcherQueuePriority priority, bool runImmediatelyIfPossible)
    {
        if (TestSettings.UseCurrentThreadAsMainThread)
        {
            return action();
        }

        DispatcherQueue? dispatcher = GetMainThreadDispatcher();
        if (dispatcher is null)
        {
            return Task.CompletedTask;
        }

        if (runImmediatelyIfPossible && dispatcher.HasThreadAccess)
        {
            try
            {
                return action();
            }
            catch (Exception e)
            {
                return Task.FromException(e);
            }
        }

        var taskCompletionSource = new TaskCompletionSource<object?>();
        bool success = dispatcher.TryEnqueue(priority, async delegate
        {
            try
            {
                await action();
                taskCompletionSource.SetResult(null);
            }
            catch (Exception e)
            {
                taskCompletionSource.SetException(e);
            }
        });

        if (!success)
        {
            taskCompletionSource.SetException(new InvalidOperationException("Failed to enqueue the operation"));
        }

        return taskCompletionSource.Task;
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
