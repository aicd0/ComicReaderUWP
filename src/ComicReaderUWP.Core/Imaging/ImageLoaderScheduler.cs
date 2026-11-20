// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;

using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Core.Common.Utils;

namespace ComicReaderUWP.Core.Imaging;

internal static class ImageLoaderScheduler
{
    private static readonly ConcurrentDictionary<string, GroupState> sGroups = new();
    private static readonly ConcurrentDictionary<string, int> sMemoryCacheRequests = [];
    private static long sSequence;

    static ImageLoaderScheduler()
    {
        ImageMemoryCache.Instance.EntryAdded += uri =>
        {
            foreach (GroupState state in sGroups.Values)
            {
                state.FlushBufferRequest(uri);
            }
        };
    }

    public static long NewSequence()
    {
        return Interlocked.Increment(ref sSequence);
    }

    public static async Task<Stream?> AcquireBuffer(Request request, string? fingerprint, Func<Task<Stream?>> openStream)
    {
        ArgumentException.ThrowIfNullOrEmpty(request.Uri);

        string uri = request.Uri;
        Stream? cached = ImageMemoryCache.Instance.TryAcquire(uri, fingerprint);
        if (cached is not null)
        {
            return cached;
        }

        QueuedRead item = new(() => ImageMemoryCache.Instance.AcquireAsync(uri, new()
        {
            Fingerprint = fingerprint,
            OpenStream = openStream,
            Priority = request.Priority,
        }), request);
        Enqueue(item, request);
        return await item.Task;
    }

    public static void InvalidateBuffer(string uri)
    {
        ImageMemoryCache.Instance.Invalidate(uri);
    }

    public static void SetBufferCapacity(long bytes)
    {
        ImageMemoryCache.Instance.Capacity = bytes;
    }

    public static void ClearBuffer()
    {
        ImageMemoryCache.Instance.Clear();
    }

    public static bool HasBufferRequest(string uri)
    {
        return sMemoryCacheRequests.TryGetValue(uri, out int count) && count > 0;
    }

    internal static bool HasBuffer(string uri)
    {
        return ImageMemoryCache.Instance.Contains(uri);
    }

    private static void RemoveBufferRequest(string uri)
    {
        while (sMemoryCacheRequests.TryGetValue(uri, out int count))
        {
            if (count > 1)
            {
                if (sMemoryCacheRequests.TryUpdate(uri, count - 1, count))
                {
                    return;
                }
            }
            else if (sMemoryCacheRequests.TryRemove(new KeyValuePair<string, int>(uri, count)))
            {
                return;
            }
        }
    }

    private static void Enqueue(IQueuedTask item, Request request)
    {
        GroupState state = sGroups.GetOrAdd(request.Group.Name, _ => new GroupState(request.Group.MaxConcurrentTasks));
        state.Enqueue(item, request.Priority);
    }

    internal readonly record struct Request
    {
        public required ImageLoaderSchedulerGroup Group { get; init; }

        public int Priority { get; init; }

        public string? Uri { get; init; }

        public long Sequence { get; init; }

        public CancellationSession.IToken? Token { get; init; }

        public bool IsCancellationRequested => Token?.IsCancellationRequested == true;
    }

    private sealed class GroupState(int maxConcurrentTasks)
    {
        private readonly Lock _lock = new();
        private readonly SortedDictionary<int, SortedDictionary<long, IQueuedTask>> _queue = [];
        private readonly int _maxConcurrentTasks = maxConcurrentTasks;
        private int _activeWorkers = 0;

        public void Enqueue(IQueuedTask item, int priority)
        {
            bool shouldStartWorker;

            lock (_lock)
            {
                if (!_queue.TryGetValue(priority, out SortedDictionary<long, IQueuedTask>? bucket))
                {
                    bucket = [];
                    _queue.Add(priority, bucket);
                }

                bucket.Add(item.Sequence, item);

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

        public void FlushBufferRequest(string uri)
        {
            List<(int Priority, long Sequence, IQueuedTask Item)> matches = [];

            lock (_lock)
            {
                foreach ((int priority, SortedDictionary<long, IQueuedTask> bucket) in _queue)
                {
                    foreach ((long sequence, IQueuedTask item) in bucket)
                    {
                        if (item.Uri == uri)
                        {
                            matches.Add((priority, sequence, item));
                        }
                    }
                }

                foreach ((int priority, long sequence, _) in matches)
                {
                    if (_queue.TryGetValue(priority, out SortedDictionary<long, IQueuedTask>? bucket))
                    {
                        bucket.Remove(sequence);
                    }
                }
            }

            foreach ((_, _, IQueuedTask item) in matches)
            {
                Dispatch(item);
            }
        }

        private async Task RunWorker()
        {
            while (true)
            {
                IQueuedTask? next = TakeNext();
                if (next is null)
                {
                    return;
                }

                if (next.Uri is not null && HasBuffer(next.Uri))
                {
                    Dispatch(next);
                    continue;
                }

                await next.ExecuteAsync();
            }
        }

        private IQueuedTask? TakeNext()
        {
            lock (_lock)
            {
                while (_queue.Count > 0)
                {
                    int highestPriority = _queue.Keys.Last();
                    SortedDictionary<long, IQueuedTask> bucket = _queue[highestPriority];
                    if (bucket.Count == 0)
                    {
                        _queue.Remove(highestPriority);
                        continue;
                    }

                    KeyValuePair<long, IQueuedTask> first = bucket.First();
                    bucket.Remove(first.Key);
                    return first.Value;
                }

                _activeWorkers--;
                return null;
            }
        }

        private static void Dispatch(IQueuedTask item)
        {
            TaskDispatcher.DefaultThreadPool.SubmitAsync(item.ExecuteAsync);
        }
    }

    private interface IQueuedTask
    {
        long Sequence { get; }

        string? Uri { get; }

        Task ExecuteAsync();
    }

    private abstract class QueuedItem : IQueuedTask
    {
        private int _started;

        protected QueuedItem(Request request)
        {
            Sequence = request.Sequence;
            Uri = request.Uri;
            Token = request.Token;

            if (request.Uri is not null)
            {
                sMemoryCacheRequests.AddOrUpdate(request.Uri, 1, (_, count) => count + 1);
            }
        }

        public long Sequence { get; }

        public string? Uri { get; }

        public CancellationSession.IToken? Token { get; }

        public abstract Task ExecuteAsync();

        protected void Begin()
        {
            if (Interlocked.Exchange(ref _started, 1) != 0)
            {
                return;
            }

            if (Uri is not null)
            {
                RemoveBufferRequest(Uri);
            }
        }
    }

    private sealed class QueuedRead(Func<Task<Stream?>> func, Request request) : QueuedItem(request)
    {
        private readonly TaskCompletionSource<Stream?> _source = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<Stream?> Task => _source.Task;

        public override async Task ExecuteAsync()
        {
            Begin();

            if (Token?.IsCancellationRequested == true)
            {
                _source.SetResult(null);
                return;
            }

            try
            {
                _source.SetResult(await func());
            }
            catch (Exception ex)
            {
                _source.SetException(ex);
            }
        }
    }
}
