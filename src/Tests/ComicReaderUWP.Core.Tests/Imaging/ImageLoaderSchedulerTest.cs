// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Core.Imaging;

namespace ComicReaderUWP.Core.Tests.Imaging;

/// <summary>
/// Queued work is reserved for I/O. Requests whose bytes are already in memory only decode, so they must not
/// wait for the device.
/// </summary>
[TestFixture]
public class ImageLoaderSchedulerTest
{
    private string _uri = string.Empty;

    [SetUp]
    public void SetUp()
    {
        ImageLoaderScheduler.ClearBuffer();
        _uri = "fake://" + Guid.NewGuid().ToString("N");
    }

    [TearDown]
    public void TearDown()
    {
        ImageLoaderScheduler.ClearBuffer();
    }

    private static ImageLoaderSchedulerGroup CreateGroup()
    {
        return ImageLoaderSchedulerGroup.FromPath(@"\\device-" + Guid.NewGuid().ToString("N") + @"\share");
    }

    private static ImageLoaderScheduler.Request CreateRequest(ImageLoaderSchedulerGroup group)
    {
        return new() { Group = group, Sequence = ImageLoaderScheduler.NewSequence() };
    }

    /// <summary>Starts a read that finishes only when <paramref name="gate"/> is released, occupying the device.</summary>
    private static Task<Stream?> BlockDeviceAsync(ImageLoaderSchedulerGroup group, TaskCompletionSource<Stream?> gate)
    {
        ImageLoaderScheduler.Request request = new()
        {
            Group = group,
            Uri = "fake://blocker-" + Guid.NewGuid().ToString("N"),
            Sequence = ImageLoaderScheduler.NewSequence(),
        };

        return ImageLoaderScheduler.AcquireBuffer(request, fingerprint: null, () => gate.Task);
    }

    /// <summary>Queues a read of a fresh uri, reporting the order in which reads actually happen.</summary>
    private static Task<Stream?> ReadAsync(ImageLoaderSchedulerGroup group, long sequence, Action onRead, int priority = 0,
        CancellationSession.IToken? token = null)
    {
        byte[] content = new byte[16];
        ImageLoaderScheduler.Request request = new()
        {
            Group = group,
            Priority = priority,
            Uri = "fake://read-" + Guid.NewGuid().ToString("N"),
            Sequence = sequence,
            Token = token,
        };

        return ImageLoaderScheduler.AcquireBuffer(request, fingerprint: null, () =>
        {
            onRead();
            return Task.FromResult<Stream?>(new MemoryStream(content, writable: false));
        });
    }

    /// Submits a leaf that reads <paramref name="uri"/>, i.e. one that other requests can be freed by.
    private static Task<Stream?> TakeAsync(ImageLoaderSchedulerGroup group, string uri)
    {
        byte[] content = new byte[16];
        ImageLoaderScheduler.Request request = new() { Group = group, Uri = uri, Sequence = ImageLoaderScheduler.NewSequence() };
        return ImageLoaderScheduler.AcquireBuffer(request, fingerprint: null,
            () => Task.FromResult<Stream?>(new MemoryStream(content, writable: false)));
    }

    private static async Task MakeResidentAsync(string uri)
    {
        // A group of its own: the caller's device may be deliberately blocked.
        using Stream? stream = await TakeAsync(CreateGroup(), uri);

        Assert.That(stream, Is.Not.Null);
        Assert.That(ImageLoaderScheduler.HasBuffer(uri), Is.True);
    }

    [Test]
    public async Task PendingContentRequest_IsTrackedWhileQueued()
    {
        ImageLoaderSchedulerGroup group = CreateGroup();
        TaskCompletionSource<Stream?> blocker = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Stream?> blocking = BlockDeviceAsync(group, blocker);

        Task<Stream?> pixel = TakeAsync(group, _uri);
        Assert.That(ImageLoaderScheduler.HasBufferRequest(_uri), Is.True);

        blocker.SetResult(null);
        await blocking;

        using Stream? stream = await pixel;
        Assert.That(stream, Is.Not.Null);
        Assert.That(ImageLoaderScheduler.HasBufferRequest(_uri), Is.False);
    }

    [Test]
    public async Task QueuedRequest_LeavesTheDeviceQueueOnceBytesBecomeResident()
    {
        ImageLoaderSchedulerGroup group = CreateGroup();
        TaskCompletionSource<Stream?> blocker = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Stream?> blocking = BlockDeviceAsync(group, blocker);

        Task<Stream?> queued = TakeAsync(group, _uri);

        // Answering the read of another consumer, e.g. a metadata request absorbing this one.
        await MakeResidentAsync(_uri);

        using Stream? stream = await queued.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(stream, Is.Not.Null);
        Assert.That(blocking.IsCompleted, Is.False, "the queued task must not have waited for the device");

        blocker.SetResult(null);
        await blocking;
    }

    [Test]
    public async Task CancelledRead_NeverOpensTheSource()
    {
        ImageLoaderSchedulerGroup group = CreateGroup();
        TaskCompletionSource<Stream?> blocker = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Stream?> blocking = BlockDeviceAsync(group, blocker);

        CancellationSession cancellation = new();
        CancellationSession.IToken token = cancellation.Token;
        cancellation.Next();

        int opens = 0;
        Task<Stream?> read = ReadAsync(group, ImageLoaderScheduler.NewSequence(), () => ++opens, token: token);

        blocker.SetResult(null);
        await blocking;
        using Stream? stream = await read.WaitAsync(TimeSpan.FromSeconds(5));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stream, Is.Null);
            Assert.That(opens, Is.Zero, "a read whose request was cancelled must not touch the device");
        }
    }

    [Test]
    public async Task Leaves_RunInSequenceOrderRegardlessOfArrival()
    {
        ImageLoaderSchedulerGroup group = CreateGroup();
        TaskCompletionSource<Stream?> blocker = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Stream?> blocking = BlockDeviceAsync(group, blocker);

        long first = ImageLoaderScheduler.NewSequence();
        long second = ImageLoaderScheduler.NewSequence();

        List<int> order = [];
        Task<Stream?> later = ReadAsync(group, second, () => Record(order, 2));
        Task<Stream?> earlier = ReadAsync(group, first, () => Record(order, 1));

        blocker.SetResult(null);
        await blocking;
        using Stream? earlierStream = await earlier;
        using Stream? laterStream = await later;

        Assert.That(order, Is.EqualTo(new int[] { 1, 2 }), "the earlier request is served first however its read arrived");
    }

    [Test]
    public async Task Leaves_HigherPriorityWinsRegardlessOfSequence()
    {
        ImageLoaderSchedulerGroup group = CreateGroup();
        TaskCompletionSource<Stream?> blocker = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Stream?> blocking = BlockDeviceAsync(group, blocker);

        List<int> order = [];
        Task<Stream?> low = ReadAsync(group, ImageLoaderScheduler.NewSequence(), () => Record(order, ImageLoadingPriorityLow), ImageLoadingPriorityLow);
        Task<Stream?> high = ReadAsync(group, ImageLoaderScheduler.NewSequence(), () => Record(order, ImageLoadingPriorityHigh), ImageLoadingPriorityHigh);

        blocker.SetResult(null);
        await blocking;
        using Stream? lowStream = await low;
        using Stream? highStream = await high;

        Assert.That(order, Is.EqualTo(new int[] { ImageLoadingPriorityHigh, ImageLoadingPriorityLow }));
    }

    private const int ImageLoadingPriorityLow = 0;
    private const int ImageLoadingPriorityHigh = 1;

    private static void Record(List<int> order, int value)
    {
        lock (order)
        {
            order.Add(value);
        }
    }
}
