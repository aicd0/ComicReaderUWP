// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;

using ComicReaderUWP.Core.Common.DebugTools;

namespace ComicReaderUWP.Core.Imaging;

internal sealed partial class ImageMemoryCache
{
    private const string TAG = nameof(ImageMemoryCache);
    private const long DEFAULT_CAPACITY = 128L * 1024 * 1024;
    private const long DEFAULT_MAX_ENTRY_SIZE = 24L * 1024 * 1024;

    internal static ImageMemoryCache Instance { get; } = new();

    internal event Action<string>? EntryAdded;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, Entry> _entries = [];
    private readonly Dictionary<string, Pending> _pending = [];
    private readonly LinkedList<Entry> _lru = [];
    private long _size;

    internal long Capacity { get; set; } = DEFAULT_CAPACITY;

    internal long MaxEntrySize { get; set; } = DEFAULT_MAX_ENTRY_SIZE;

    internal long ApproximateSize
    {
        get
        {
            lock (_lock)
            {
                return _size;
            }
        }
    }

    internal int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    internal bool Contains(string key)
    {
        lock (_lock)
        {
            return _entries.ContainsKey(key);
        }
    }

    internal async Task<Stream?> AcquireAsync(string key, Request request)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(request);

        Pending? pending;
        bool claimed = false;
        lock (_lock)
        {
            if (_entries.TryGetValue(key, out Entry? hit))
            {
                if (hit.Matches(request.Fingerprint))
                {
                    hit.RefCount++;
                    _lru.Remove(hit.Node);
                    _lru.AddFirst(hit.Node);
                    LogResidentHit(key);
                    return new SharedContentStream(this, hit);
                }

                Drop(hit, EvictionReason.Stale);
            }

            if (!_pending.TryGetValue(key, out pending))
            {
                pending = new Pending();
                _pending.Add(key, pending);
                claimed = true;
            }

            pending.WaiterCount++;
        }

        if (claimed)
        {
            _ = ReadAsync(key, request, pending);
        }
        else
        {
            LogInFlightHit(key);
        }

        Entry? entry = await pending.Completion.Task;
        return entry is null ? null : new SharedContentStream(this, entry);
    }

    internal Stream? TryAcquire(string key, string? fingerprint)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out Entry? entry))
            {
                return null;
            }

            if (!entry.Matches(fingerprint))
            {
                Drop(entry, EvictionReason.Stale);
                return null;
            }

            entry.RefCount++;
            _lru.Remove(entry.Node);
            _lru.AddFirst(entry.Node);
            LogResidentHit(key);
            return new SharedContentStream(this, entry);
        }
    }

    internal void Invalidate(string key)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(key, out Entry? entry))
            {
                Drop(entry, EvictionReason.Stale);
            }
        }
    }

    internal void Clear()
    {
        lock (_lock)
        {
            if (_entries.Count > 0)
            {
                LogClear(_entries.Count, _size);
            }

            foreach (Entry entry in _entries.Values)
            {
                entry.Condemned = true;
                if (entry.RefCount <= 0)
                {
                    entry.Free();
                }
            }

            _entries.Clear();
            _lru.Clear();
            _size = 0;
        }
    }

    private async Task ReadAsync(string key, Request request, Pending pending)
    {
        Entry? entry = null;
        bool published = false;
        long startTime = Stopwatch.GetTimestamp();
        try
        {
            byte[]? buffer = await ReadAllAsync(request);

            if (buffer is null || buffer.Length == 0)
            {
                LogRead(key, 0, Stopwatch.GetElapsedTime(startTime), pending.WaiterCount, ApproximateSize, request.Priority);
                return;
            }

            int consumers = pending.WaiterCount;
            long cachedBytes;
            lock (_lock)
            {
                if (consumers > 0)
                {
                    entry = new Entry(key, buffer, request.Fingerprint)
                    {
                        RefCount = consumers,
                    };
                    published = Publish(entry);
                }

                cachedBytes = _size;
            }

            LogRead(key, buffer.Length, Stopwatch.GetElapsedTime(startTime), consumers, cachedBytes, request.Priority);
        }
        catch (Exception ex)
        {
            Logger.E(TAG, ex);
        }
        finally
        {
            lock (_lock)
            {
                _pending.Remove(key);
            }

            pending.Completion.TrySetResult(entry);
        }

        if (published)
        {
            EntryAdded?.Invoke(key);
        }
    }

    private static async Task<byte[]?> ReadAllAsync(Request request)
    {
        Stream? stream = await request.OpenStream();
        if (stream is null)
        {
            return null;
        }

        try
        {
            if (stream.CanSeek)
            {
                long length = stream.Length;
                if (length <= 0 || length > int.MaxValue)
                {
                    return null;
                }

                byte[] seekableBuffer = new byte[(int)length];
                stream.Position = 0;
                await stream.ReadExactlyAsync(seekableBuffer);
                return seekableBuffer;
            }

            using MemoryStream memory = new();
            await stream.CopyToAsync(memory);
            return memory.ToArray();
        }
        finally
        {
            stream.Dispose();
        }
    }

    private bool Publish(Entry entry)
    {
        long size = entry.Buffer.Length;
        if (Capacity <= 0 || size > MaxEntrySize || size > Capacity)
        {
            return false;
        }

        while (_size + size > Capacity)
        {
            if (!EvictOne())
            {
                return false;
            }
        }

        _entries[entry.Key] = entry;
        _lru.AddFirst(entry.Node);
        _size += size;
        return true;
    }

    private void Drop(Entry entry, EvictionReason reason)
    {
        if (entry.Condemned)
        {
            return;
        }

        entry.Condemned = true;
        _entries.Remove(entry.Key);
        _lru.Remove(entry.Node);
        _size -= entry.Buffer.Length;
        LogEviction(entry.Key, entry.Buffer.Length, _size, reason);

        if (entry.RefCount <= 0)
        {
            entry.Free();
        }
    }

    private bool EvictOne()
    {
        for (LinkedListNode<Entry>? node = _lru.Last; node is not null; node = node.Previous)
        {
            Entry entry = node.Value;
            if (entry.RefCount > 0)
            {
                continue;
            }

            Drop(entry, EvictionReason.LeastRecentlyUsed);
            return true;
        }

        return false;
    }

    private void Release(Entry entry)
    {
        lock (_lock)
        {
            if (entry.RefCount > 0)
            {
                entry.RefCount--;
            }

            if (entry.RefCount <= 0 && entry.Condemned)
            {
                entry.Free();
            }
        }
    }

    private static void LogRead(string key, long bytes, TimeSpan elapsed, int consumers, long cachedBytes, int priority)
    {
        string consumersText = consumers == 1 ? "1 consumer" : $"{consumers} consumers";
        Logger.I(TAG, $"read [buffered] {key} ({FormatBytes(bytes)}, {elapsed.TotalMilliseconds:F0} ms, {consumersText}, priority {priority}, cache {FormatBytes(cachedBytes)})");
    }

    private static void LogResidentHit(string key)
    {
        Logger.I(TAG, $"hit [resident] {key}");
    }

    private static void LogInFlightHit(string key)
    {
        Logger.I(TAG, $"hit [in-flight] {key}");
    }

    private static void LogEviction(string key, long bytes, long cachedBytes, EvictionReason reason)
    {
        string reasonText = reason switch
        {
            EvictionReason.LeastRecentlyUsed => "least-recently-used",
            EvictionReason.Stale => "stale",
            _ => "unknown",
        };

        Logger.I(TAG, $"evict [{reasonText}] {key} ({FormatBytes(bytes)}, cache {FormatBytes(cachedBytes)})");
    }

    private static void LogClear(int entries, long bytes)
    {
        string entriesText = entries == 1 ? "1 entry" : $"{entries} entries";
        Logger.I(TAG, $"evict [cleared] {entriesText} ({FormatBytes(bytes)})");
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        return bytes >= 1024 * 1024
            ? $"{bytes / (1024.0 * 1024.0):F1} MiB"
            : $"{bytes / 1024.0:F1} KiB";
    }

    private enum EvictionReason
    {
        LeastRecentlyUsed,
        Stale,
    }

    internal sealed class Request
    {
        public required Func<Task<Stream?>> OpenStream { get; init; }
        public string? Fingerprint { get; init; }
        public int Priority { get; init; }
    }

    private sealed partial class Entry(string key, byte[] buffer, string? fingerprint)
    {
        private LinkedListNode<Entry>? _node;

        public string Key { get; } = key;

        public string? Fingerprint { get; } = fingerprint;

        public byte[] Buffer { get; private set; } = buffer;

        public int RefCount { get; set; }

        public bool Condemned { get; set; }

        public LinkedListNode<Entry> Node => _node ??= new LinkedListNode<Entry>(this);

        public bool Matches(string? fingerprint)
        {
            return fingerprint is null || Fingerprint == fingerprint;
        }

        public void Free()
        {
            Buffer = [];
        }
    }

    private sealed partial class Pending
    {
        public int WaiterCount { get; set; }

        public TaskCompletionSource<Entry?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed partial class SharedContentStream(ImageMemoryCache registry, Entry entry)
        : MemoryStream(entry.Buffer, 0, entry.Buffer.Length, writable: false)
    {
        private int _released = 0;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing && Interlocked.Exchange(ref _released, 1) == 0)
            {
                registry.Release(entry);
            }
        }
    }
}
