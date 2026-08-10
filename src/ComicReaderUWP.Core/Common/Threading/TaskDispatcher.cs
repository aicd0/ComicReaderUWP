// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;

using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Utils;

namespace ComicReaderUWP.Core.Common.Threading;

public abstract partial class TaskDispatcher(string name) : ITaskDispatcher
{
    public static ITaskDispatcher DefaultQueue { get; } = Factory.NewQueue("DefaultQueue");
    public static ITaskDispatcher DefaultThreadPool { get; } = Factory.NewThreadPool("DefaultThreadPool");
    public static ITaskDispatcher LongRunningThreadPool { get; } = new ThreadPoolDispatcher("LongRunningThreadPool", TaskCreationOptions.LongRunning);

    private readonly string _name = name;

    public Task Submit(Action action)
    {
        return SubmitAsync(async () =>
        {
            action();
        });
    }

    public Task<R> Submit<R>(Func<R> func)
    {
        return SubmitAsync(async () =>
        {
            return func();
        });
    }

    public Task SubmitAsync(Func<Task> func)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SubmitInternal(async () =>
        {
            try
            {
                await func();
            }
            catch (Exception ex)
            {
                DebugUtils.CaptureFatalError($"An unknown error occurred in task dispatcher '{_name}'.", ex);
                source.SetException(ex);
                return;
            }

            source.SetResult();
        });

        return source.Task;
    }

    public Task<R> SubmitAsync<R>(Func<Task<R>> func)
    {
        var source = new TaskCompletionSource<R>(TaskCreationOptions.RunContinuationsAsynchronously);
        SubmitInternal(async () =>
        {
            R result;
            try
            {
                result = await func();
            }
            catch (Exception ex)
            {
                DebugUtils.CaptureFatalError($"An unknown error occurred in task dispatcher '{_name}'.", ex);
                source.SetException(ex);
                return;
            }

            source.SetResult(result);
        });

        return source.Task;
    }

    protected abstract void SubmitInternal(Func<Task> func);

    private class QueueDispatcher(string name) : TaskDispatcher(name)
    {
        private readonly ConcurrentQueue<Func<Task>> _queue = [];
        private readonly Lock _lock = new();
        private bool _postDequeueTask = false;

        protected override void SubmitInternal(Func<Task> func)
        {
            _queue.Enqueue(func);
            bool postDequeueTask;
            lock (_lock)
            {
                postDequeueTask = !_postDequeueTask;
                _postDequeueTask = true;
            }

            if (postDequeueTask)
            {
                Task.Run(Dequeue);
            }
        }

        private async Task Dequeue()
        {
            while (true)
            {
                while (_queue.TryDequeue(out Func<Task>? func))
                {
                    await func();
                }

                bool canExit;
                lock (_lock)
                {
                    canExit = _queue.IsEmpty;
                    _postDequeueTask = !canExit;
                }

                if (canExit)
                {
                    break;
                }
            }
        }
    }

    private class ThreadPoolDispatcher(string name, TaskCreationOptions creationOptions) : TaskDispatcher(name)
    {
        private readonly TaskCreationOptions _creationOptions = creationOptions;

        protected override void SubmitInternal(Func<Task> func)
        {
            Task.Factory.StartNew(() =>
            {
                CoroutineUtils.Run(func);
            }, default, _creationOptions, TaskScheduler.Default);
        }
    }

    private partial class SingleThreadDispatcher : TaskDispatcher, IDisposableTaskDispatcher
    {
        private readonly BlockingCollection<Func<Task>> _queue = [];
        private readonly Thread _thread;

        public SingleThreadDispatcher(string name) : base(name)
        {
            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = name
            };
            _thread.Start();
        }

        protected override void SubmitInternal(Func<Task> func)
        {
            _queue.Add(func);
        }

        private void Run()
        {
            CoroutineUtils.Run(async () =>
            {
                foreach (Func<Task> action in _queue.GetConsumingEnumerable())
                {
                    await action();
                }
            });
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
            _thread.Join();
            _queue.Dispose();
        }
    }

    public static class Factory
    {
        public static ITaskDispatcher NewQueue(string name)
        {
            return new QueueDispatcher(name);
        }

        public static ITaskDispatcher NewThreadPool(string name)
        {
            return new ThreadPoolDispatcher(name, TaskCreationOptions.PreferFairness);
        }

        /**
         * <summary>
         * Creates a new single-threaded dispatcher.
         * </summary>
         * <remarks>
         * This dispatcher requests thread resource from system. If possible, use the queue dispatcher instead.
         * </remarks>
         */
        public static IDisposableTaskDispatcher NewSingleThread(string name)
        {
            return new SingleThreadDispatcher(name);
        }
    }
}
