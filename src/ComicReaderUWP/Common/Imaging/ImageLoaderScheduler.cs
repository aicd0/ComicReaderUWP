// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ComicReaderUWP.Core.Common.Threading;

namespace ComicReaderUWP.Common.Imaging;

internal static class ImageLoaderScheduler
{
    private static readonly ConcurrentDictionary<string, GroupState> sGroups = new();

    public static Task Submit(Func<Task> func, ImageLoaderSchedulerGroup group, int priority)
    {
        GroupState state = sGroups.GetOrAdd(group.Name, _ => new GroupState(group.MaxConcurrentTasks));
        QueuedTask item = new(func);
        state.Enqueue(item, priority);
        return item.Task;
    }

    public static Task<T> Submit<T>(Func<Task<T>> func, ImageLoaderSchedulerGroup group, int priority)
    {
        GroupState state = sGroups.GetOrAdd(group.Name, _ => new GroupState(group.MaxConcurrentTasks));
        QueuedTask<T> item = new(func);
        state.Enqueue(item, priority);
        return item.Task;
    }

    private sealed class GroupState(int maxConcurrentTasks)
    {
        private readonly Lock _lock = new();
        private readonly SortedDictionary<int, Queue<IQueuedTask>> _queue = [];
        private readonly int _maxConcurrentTasks = maxConcurrentTasks;
        private int _activeWorkers = 0;

        public void Enqueue(IQueuedTask item, int priority)
        {
            bool shouldStartWorker;

            lock (_lock)
            {
                if (!_queue.TryGetValue(priority, out Queue<IQueuedTask>? bucket))
                {
                    bucket = new Queue<IQueuedTask>();
                    _queue.Add(priority, bucket);
                }

                bucket.Enqueue(item);

                if (_activeWorkers < _maxConcurrentTasks)
                {
                    _activeWorkers++;
                    shouldStartWorker = true;
                }
                else
                {
                    shouldStartWorker = false;
                }
            }

            if (shouldStartWorker)
            {
                TaskDispatcher.DefaultThreadPool.SubmitAsync(RunWorker);
            }
        }

        private async Task RunWorker()
        {
            while (true)
            {
                IQueuedTask? next;
                lock (_lock)
                {
                    if (_queue.Count == 0)
                    {
                        _activeWorkers--;
                        return;
                    }

                    int highestPriority = _queue.Keys.Last();
                    Queue<IQueuedTask> bucket = _queue[highestPriority];
                    next = bucket.Dequeue();
                    if (bucket.Count == 0)
                    {
                        _queue.Remove(highestPriority);
                    }
                }

                await next.ExecuteAsync();
            }
        }
    }

    private interface IQueuedTask
    {
        Task ExecuteAsync();
    }

    private sealed class QueuedTask(Func<Task> func) : IQueuedTask
    {
        private readonly Func<Task> _func = func;
        private readonly TaskCompletionSource _source = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Task => _source.Task;

        public async Task ExecuteAsync()
        {
            try
            {
                await _func();
                _source.SetResult();
            }
            catch (Exception ex)
            {
                _source.SetException(ex);
            }
        }
    }

    private sealed class QueuedTask<T>(Func<Task<T>> func) : IQueuedTask
    {
        private readonly Func<Task<T>> _func = func;
        private readonly TaskCompletionSource<T> _source = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<T> Task => _source.Task;

        public async Task ExecuteAsync()
        {
            try
            {
                _source.SetResult(await _func());
            }
            catch (Exception ex)
            {
                _source.SetException(ex);
            }
        }
    }
}
