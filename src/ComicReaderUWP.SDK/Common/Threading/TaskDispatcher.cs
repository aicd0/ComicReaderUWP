// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;

using ComicReaderUWP.SDK.Common.DebugTools;

namespace ComicReaderUWP.SDK.Common.Threading;

public abstract class TaskDispatcher : ITaskDispatcher
{
    private const string TAG = "TaskDispatcher";

    public static ITaskDispatcher DefaultQueue { get; } = Factory.NewQueue("DefaultQueue");
    public static ITaskDispatcher DefaultThreadPool { get; } = Factory.NewThreadPool("DefaultThreadPool");
    public static ITaskDispatcher LongRunningThreadPool { get; } = new ThreadPoolDispatcher("LongRunningThreadPool", TaskCreationOptions.LongRunning);

    private readonly string _name;
    private readonly LogTag _submitTag;
    private readonly LogTag _startTag;
    private readonly LogTag _endTag;
    private int _pendingTaskCount = 0;
    private int _runningTaskCount = 0;

    protected TaskDispatcher(string name)
    {
        _name = name;
        _submitTag = LogTag.N(TAG, "submit", _name);
        _startTag = LogTag.N(TAG, "start", _name);
        _endTag = LogTag.N(TAG, "end", _name);
    }

    public void Submit(string taskName, Action action)
    {
        ArgumentNullException.ThrowIfNull(taskName, nameof(taskName));
        ArgumentNullException.ThrowIfNull(action, nameof(action));

        long submitTime = GetCurrentMilliseconds();
        {
            int pendingCount = Interlocked.Increment(ref _pendingTaskCount);
            int runningCount = _runningTaskCount;
            Log(_submitTag, $"task={taskName},running={runningCount},pending={pendingCount}");
        }

        SubmitInternal(() =>
        {
            try
            {
                long startTime = GetCurrentMilliseconds();
                {
                    long since0 = GetCurrentMilliseconds() - submitTime;
                    int pendingCount = Interlocked.Decrement(ref _pendingTaskCount);
                    int runningCount = Interlocked.Increment(ref _runningTaskCount);
                    Log(_startTag, $"task={taskName},since0={since0},running={runningCount},pending={pendingCount}");
                }

                try
                {
                    action();
                }
                finally
                {
                    int pendingCount = _pendingTaskCount;
                    int runningCount = Interlocked.Decrement(ref _runningTaskCount);
                    long time = GetCurrentMilliseconds();
                    long since0 = time - submitTime;
                    long since1 = time - startTime;
                    Log(_endTag, $"task={taskName},since0={since0},since1={since1},running={runningCount},pending={pendingCount}");
                }
            }
            catch (Exception e)
            {
                DebugUtils.CaptureFatalError($"Task '{taskName}' throwed an exception.", e);
            }
        });
    }

    protected abstract void SubmitInternal(Action action);

    private static void Log(LogTag tag, string message)
    {
        // Do nothing for now
    }

    private static long GetCurrentMilliseconds()
    {
        return DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }

    private class QueueDispatcher(string name) : TaskDispatcher(name)
    {
        private readonly ConcurrentQueue<Action> _queue = [];
        private readonly object _lock = new();
        private bool _postDequeueTask = false;

        protected override void SubmitInternal(Action action)
        {
            _queue.Enqueue(action);
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

        private void Dequeue()
        {
            while (true)
            {
                while (_queue.TryDequeue(out Action? action))
                {
                    action();
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

    private class ThreadPoolDispatcher : TaskDispatcher
    {
        private readonly TaskCreationOptions _creationOptions;

        public ThreadPoolDispatcher(string name, TaskCreationOptions creationOptions) : base(name)
        {
            _creationOptions = creationOptions;
        }

        protected override void SubmitInternal(Action action)
        {
            Task.Factory.StartNew(action, default, _creationOptions, TaskScheduler.Default);
        }
    }

    private class SingleThreadDispatcher : TaskDispatcher, IDisposableTaskDispatcher
    {
        private readonly BlockingCollection<Action> _queue = [];
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

        protected override void SubmitInternal(Action action)
        {
            _queue.Add(action);
        }

        private void Run()
        {
            foreach (Action action in _queue.GetConsumingEnumerable())
            {
                action();
            }
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
