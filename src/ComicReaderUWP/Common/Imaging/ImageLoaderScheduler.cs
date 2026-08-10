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
        return state.Submit(func, priority);
    }

    private sealed class GroupState(int maxConcurrentTasks)
    {
        private readonly Lock _lock = new();
        private readonly SortedDictionary<int, Queue<QueuedTask>> _queue = [];
        private readonly int _maxConcurrentTasks = maxConcurrentTasks;
        private int _activeWorkers = 0;

        public Task Submit(Func<Task> func, int priority)
        {
            QueuedTask item = new(func);
            bool shouldStartWorker;

            lock (_lock)
            {
                if (!_queue.TryGetValue(priority, out Queue<QueuedTask>? bucket))
                {
                    bucket = new Queue<QueuedTask>();
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

            return item.Task;
        }

        private async Task RunWorker()
        {
            while (true)
            {
                QueuedTask? next;
                lock (_lock)
                {
                    if (_queue.Count == 0)
                    {
                        _activeWorkers--;
                        return;
                    }

                    int highestPriority = _queue.Keys.Last();
                    Queue<QueuedTask> bucket = _queue[highestPriority];
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

    private sealed class QueuedTask(Func<Task> func)
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
}
