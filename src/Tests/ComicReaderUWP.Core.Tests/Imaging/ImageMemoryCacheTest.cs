// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;

using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Imaging;

namespace ComicReaderUWP.Core.Tests.Imaging;

[TestFixture]
public class ImageMemoryCacheTest
{
    private sealed class FakeSource
    {
        private readonly ConcurrentDictionary<string, int> _openCounts = [];

        public ConcurrentDictionary<string, byte[]> Contents { get; } = [];

        public int OpenCount(string key)
        {
            return _openCounts.GetValueOrDefault(key);
        }

        public ImageMemoryCache.Request CreateRequest(string key, string? fingerprint = null)
        {
            return new()
            {
                Fingerprint = fingerprint,
                OpenStream = () =>
                {
                    _openCounts.AddOrUpdate(key, 1, (_, count) => count + 1);
                    byte[]? bytes = Contents.GetValueOrDefault(key);
                    return Task.FromResult<Stream?>(bytes is null ? null : new MemoryStream(bytes, writable: false));
                },
            };
        }
    }

    private static byte[] CreateBytes(int length, byte seed = 0)
    {
        byte[] bytes = new byte[length];
        for (int i = 0; i < length; ++i)
        {
            bytes[i] = (byte)(seed + i);
        }

        return bytes;
    }

    private static ImageMemoryCache CreateRegistry(long capacity)
    {
        return new ImageMemoryCache
        {
            Capacity = capacity,
            MaxEntrySize = capacity,
        };
    }

    private static Stream RequireStream(Stream? stream)
    {
        Assert.That(stream, Is.Not.Null);
        return stream ?? throw new InvalidOperationException("Expected a stream from the image source.");
    }

    private static async Task<Stream> AcquireAsync(ImageMemoryCache registry, FakeSource source, string key)
    {
        return RequireStream(await registry.AcquireAsync(key, source.CreateRequest(key)));
    }

    private static async Task<Stream> RequireStreamAsync(Task<Stream?> task)
    {
        return RequireStream(await task);
    }

    private static async Task<byte[]> ReadAndDisposeAsync(Stream stream)
    {
        using (stream)
        {
            using MemoryStream buffer = new();
            await stream.CopyToAsync(buffer);
            return buffer.ToArray();
        }
    }

    [Test]
    public async Task ConcurrentAcquire_ReadsSourceOnce()
    {
        ImageMemoryCache registry = CreateRegistry(1024);
        byte[] content = CreateBytes(64);
        int openCount = 0;

        Task<Stream?> AcquireAsync()
        {
            return registry.AcquireAsync("page", new()
            {
                OpenStream = async () =>
                {
                    Interlocked.Increment(ref openCount);
                    await Task.Delay(50);
                    return new MemoryStream(content, writable: false);
                },
            });
        }

        Task<Stream?>[] tasks = [AcquireAsync(), AcquireAsync(), AcquireAsync(), AcquireAsync()];
        Stream?[] streams = await Task.WhenAll(tasks);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(openCount, Is.EqualTo(1));
            Assert.That(registry.Count, Is.EqualTo(1));
        }

        foreach (Stream? stream in streams)
        {
            Assert.That(stream, Is.Not.Null);
            Assert.That(await ReadAndDisposeAsync(RequireStream(stream)), Is.EqualTo(content));
        }
    }

    [Test]
    public async Task AcquireAfterRelease_DoesNotReadSourceAgain()
    {
        ImageMemoryCache registry = CreateRegistry(1024);
        FakeSource source = new();
        source.Contents["page"] = CreateBytes(64);

        byte[] first = await ReadAndDisposeAsync(await AcquireAsync(registry, source, "page"));
        byte[] second = await ReadAndDisposeAsync(await AcquireAsync(registry, source, "page"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(source.OpenCount("page"), Is.EqualTo(1));
            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(first, Is.EqualTo(source.Contents["page"]));
            Assert.That(second, Is.EqualTo(source.Contents["page"]));
        }
    }

    [Test]
    public async Task Eviction_RemovesLeastRecentlyUsed()
    {
        ImageMemoryCache registry = CreateRegistry(12);
        FakeSource source = new();
        string[] keys = ["a", "b", "c", "d"];
        foreach (string key in keys)
        {
            source.Contents[key] = CreateBytes(4, (byte)key[0]);
        }

        foreach (string key in keys[..3])
        {
            await ReadAndDisposeAsync(await AcquireAsync(registry, source, key));
        }

        await ReadAndDisposeAsync(await AcquireAsync(registry, source, "d"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(registry.Count, Is.EqualTo(3));
            Assert.That(registry.ApproximateSize, Is.EqualTo(12));
        }

        // "a" was the least recently used and had to make room for "d".
        await ReadAndDisposeAsync(await AcquireAsync(registry, source, "a"));
        Assert.That(source.OpenCount("a"), Is.EqualTo(2));

        // "b", "c" and "d" are still resident.
        using (Assert.EnterMultipleScope())
        {
            Assert.That(source.OpenCount("b"), Is.EqualTo(1));
            Assert.That(source.OpenCount("c"), Is.EqualTo(1));
            Assert.That(source.OpenCount("d"), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Eviction_SkipsEntriesInUse()
    {
        ImageMemoryCache registry = CreateRegistry(8);
        FakeSource source = new();
        source.Contents["a"] = CreateBytes(4);
        source.Contents["b"] = CreateBytes(4);
        source.Contents["c"] = CreateBytes(4);

        Stream first = await AcquireAsync(registry, source, "a");
        Stream second = await AcquireAsync(registry, source, "b");
        Stream third = await AcquireAsync(registry, source, "c");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(registry.Count, Is.EqualTo(2));
            Assert.That(registry.ApproximateSize, Is.EqualTo(8));
            Assert.That(third.Length, Is.EqualTo(4));
        }

        first.Dispose();
        second.Dispose();
        third.Dispose();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Eviction_IsLoggedWithItsReason()
    {
        ImageMemoryCache registry = CreateRegistry(4096);
        FakeSource source = new();
        string[] keys = ["page-a", "page-b", "page-c"];
        foreach (string key in keys)
        {
            source.Contents[key] = CreateBytes(2048);
        }

        ImagingLogCapture logs = new();
        Logger.AddListener(logs);
        try
        {
            foreach (string key in keys)
            {
                await ReadAndDisposeAsync(await AcquireAsync(registry, source, key));
            }

            await TestTiming.WaitUntilAsync(() => logs.EventsFor("page-a").Length == 2,
                "the read and the eviction of page-a should both be logged");

            string[] events = logs.EventsFor("page-a");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(events[0], Does.StartWith("read [buffered] page-a ("));
                Assert.That(events[1], Does.StartWith("evict [least-recently-used] page-a ("));
                Assert.That(events[1], Does.Contain(", cache "));
            }
        }
        finally
        {
            Logger.RemoveListener(logs);
        }
    }

    [Test]
    public async Task ShrinkingCapacity_EvictsImmediately()
    {
        ImageMemoryCache registry = CreateRegistry(12);
        FakeSource source = new();
        string[] keys = ["a", "b", "c"];
        foreach (string key in keys)
        {
            source.Contents[key] = CreateBytes(4, (byte)key[0]);
            await ReadAndDisposeAsync(await AcquireAsync(registry, source, key));
        }

        registry.Capacity = 4;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(registry.ApproximateSize, Is.EqualTo(4));
            Assert.That(registry.Contains("c"), Is.True);
        }

        // The evicted pages are read again, the retained one is not.
        await ReadAndDisposeAsync(await AcquireAsync(registry, source, "c"));
        Assert.That(source.OpenCount("c"), Is.EqualTo(1));
    }

    [Test]
    public async Task ShrinkingCapacity_KeepsContentReadableUntilReleased()
    {
        ImageMemoryCache registry = CreateRegistry(1024);
        FakeSource source = new();
        source.Contents["a"] = CreateBytes(4, (byte)'a');

        Stream stream = await AcquireAsync(registry, source, "a");

        registry.Capacity = 0;

        Assert.That(registry.Contains("a"), Is.False);
        Assert.That(await ReadAndDisposeAsync(stream), Is.EqualTo(CreateBytes(4, (byte)'a')));
    }

    [Test]
    public async Task Clear_KeepsContentReadableUntilReleased()
    {
        ImageMemoryCache registry = CreateRegistry(1024);
        FakeSource source = new();
        source.Contents["page"] = CreateBytes(64);

        Stream stream = await AcquireAsync(registry, source, "page");
        registry.Clear();

        byte[] content = await ReadAndDisposeAsync(stream);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(registry.Count, Is.Zero);
            Assert.That(registry.ApproximateSize, Is.Zero);
            Assert.That(content, Is.EqualTo(source.Contents["page"]));
        }

        await ReadAndDisposeAsync(await AcquireAsync(registry, source, "page"));
        Assert.That(source.OpenCount("page"), Is.EqualTo(2));
    }

    [Test]
    public async Task OversizedEntry_IsServedButNotKept()
    {
        ImageMemoryCache registry = CreateRegistry(1024);
        registry.MaxEntrySize = 4;
        FakeSource source = new();
        source.Contents["page"] = CreateBytes(64);

        byte[] content = await ReadAndDisposeAsync(await AcquireAsync(registry, source, "page"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(content, Is.EqualTo(source.Contents["page"]));
            Assert.That(registry.Count, Is.Zero);
            Assert.That(registry.ApproximateSize, Is.Zero);
        }
    }

    [Test]
    public async Task UnreadableSource_ReturnsNull()
    {
        ImageMemoryCache registry = CreateRegistry(1024);
        FakeSource source = new();

        Stream? stream = await registry.AcquireAsync("missing", source.CreateRequest("missing"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stream, Is.Null);
            Assert.That(registry.Count, Is.Zero);
        }
    }

    [Test]
    public async Task ConcurrentConsumers_GetIndependentStreamsOverOneBuffer()
    {
        ImageMemoryCache registry = CreateRegistry(1024);
        FakeSource source = new();
        source.Contents["page"] = CreateBytes(64);

        Stream first = await AcquireAsync(registry, source, "page");
        Stream second = await AcquireAsync(registry, source, "page");

        // Reading them at the same time only works if neither owns the other's position: the buffer is shared,
        // the stream never is.
        byte[][] contents = await Task.WhenAll(ReadAndDisposeAsync(first), ReadAndDisposeAsync(second));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(contents[0], Is.EqualTo(source.Contents["page"]));
            Assert.That(contents[1], Is.EqualTo(source.Contents["page"]));
            Assert.That(source.OpenCount("page"), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task IsResident_TracksRetainedBytes()
    {
        ImageMemoryCache registry = CreateRegistry(1024);
        FakeSource source = new();
        source.Contents["page"] = CreateBytes(64);

        Assert.That(registry.Contains("page"), Is.False);

        (await AcquireAsync(registry, source, "page")).Dispose();
        Assert.That(registry.Contains("page"), Is.True, "released bytes stay retained for later requests");

        registry.Clear();
        Assert.That(registry.Contains("page"), Is.False);
    }

    [Test]
    public async Task IsResident_ClearedWhenEvicted()
    {
        ImageMemoryCache registry = CreateRegistry(4);
        FakeSource first = new();
        first.Contents["a"] = CreateBytes(4);
        FakeSource second = new();
        second.Contents["b"] = CreateBytes(4);

        (await AcquireAsync(registry, first, "a")).Dispose();
        (await AcquireAsync(registry, second, "b")).Dispose();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(registry.Contains("a"), Is.False);
            Assert.That(registry.Contains("b"), Is.True);
        }
    }

    [Test]
    public async Task ChangedFingerprint_ReplacesRetainedBytes()
    {
        ImageMemoryCache registry = CreateRegistry(1024);
        FakeSource source = new();
        source.Contents["page"] = CreateBytes(64);

        (await RequireStreamAsync(registry.AcquireAsync("page", source.CreateRequest("page", "v1")))).Dispose();

        byte[] replaced = await ReadAndDisposeAsync(await RequireStreamAsync(registry.AcquireAsync("page", source.CreateRequest("page", "v2"))));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(source.OpenCount("page"), Is.EqualTo(2), "the file must be read again");
            Assert.That(replaced, Is.EqualTo(source.Contents["page"]));
        }
    }

    [Test]
    public async Task UnknownFingerprint_UsesRetainedBytes()
    {
        ImageMemoryCache registry = CreateRegistry(1024);
        FakeSource source = new();
        source.Contents["page"] = CreateBytes(64);

        (await RequireStreamAsync(registry.AcquireAsync("page", source.CreateRequest("page", "v1")))).Dispose();

        byte[] bytes = await ReadAndDisposeAsync(await RequireStreamAsync(registry.AcquireAsync("page", source.CreateRequest("page", fingerprint: null))));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(source.OpenCount("page"), Is.EqualTo(1), "an unvalidated source accepts what is retained");
            Assert.That(bytes, Is.EqualTo(source.Contents["page"]));
        }
    }
}
