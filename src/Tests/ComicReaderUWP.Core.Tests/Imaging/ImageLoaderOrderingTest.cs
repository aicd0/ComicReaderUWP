// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;

using ComicReaderUWP.Core.Imaging;

namespace ComicReaderUWP.Core.Tests.Imaging;

/// <summary>
/// Read order comes from the request, not from when its read reaches the device: an earlier request is read
/// first even when its read is queued last, and a reader page overtakes previews that are already queued.
/// </summary>
[TestFixture]
public class ImageLoaderOrderingTest
{
    // Mirrors ImageLoadingPriority of the app, which the core assembly does not know about.
    private const int PREVIEW_PRIORITY = 1;
    private const int READER_PRIORITY = 2;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private string _directory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ComicReaderUWP.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        ImageLoaderContext.InjectDatabase(new ImageCacheDatabase(Path.Combine(_directory, "Images.sqlite")));
        ImageLoaderScheduler.ClearBuffer();
    }

    [TearDown]
    public void TearDown()
    {
        ImageLoaderScheduler.ClearBuffer();

        try
        {
            Directory.Delete(_directory, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Test]
    public async Task EarlierRequest_IsReadFirstEvenWhenItsReadIsQueuedLast()
    {
        var group = ImageLoaderSchedulerGroup.FromPath(@"\\device-" + Guid.NewGuid().ToString("N") + @"\share");
        ConcurrentQueue<string> reads = new();

        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeSource earlier = new("fake://earlier", reads, gate.Task);
        FakeSource later = new("fake://later", reads);

        (TaskCompletionSource<Stream?> release, Task blocking) = await BlockDeviceAsync(group);
        try
        {
            Task<ImageContent?> earlierTask = ImageLoader.LoadImageContent(earlier, new() { SchedulerGroup = group });
            Task<ImageContent?> laterTask = ImageLoader.LoadImageContent(later, new() { SchedulerGroup = group });

            // The later request is the one that reaches the device queue first: the earlier one is still opening.
            await TestTiming.WaitUntilAsync(() => ImageLoaderScheduler.HasBufferRequest(later.Uri),
                "the later request should have queued its read");
            gate.SetResult();
            await TestTiming.WaitUntilAsync(() => ImageLoaderScheduler.HasBufferRequest(earlier.Uri),
                "the earlier request should have queued its read");

            release.SetResult(null);
            await blocking.WaitAsync(Timeout);
            await FinishAsync(earlierTask, laterTask);
        }
        finally
        {
            gate.TrySetResult();
            release.TrySetResult(null);
        }

        Assert.That(reads.ToArray(), Is.EqualTo(new[] { earlier.Uri, later.Uri }));
    }

    [Test]
    public async Task ReaderRequest_OvertakesQueuedPreview()
    {
        var group = ImageLoaderSchedulerGroup.FromPath(@"\\device-" + Guid.NewGuid().ToString("N") + @"\share");
        ConcurrentQueue<string> reads = new();
        FakeSource preview = new("fake://preview", reads);
        FakeSource reader = new("fake://reader", reads);

        (TaskCompletionSource<Stream?> release, Task blocking) = await BlockDeviceAsync(group);
        try
        {
            Task<ImageContent?> previewTask = ImageLoader.LoadImageContent(preview, new()
            {
                SchedulerGroup = group,
                Priority = PREVIEW_PRIORITY,
            });
            await TestTiming.WaitUntilAsync(() => ImageLoaderScheduler.HasBufferRequest(preview.Uri),
                "the preview should have queued its read");

            Task<ImageContent?> readerTask = ImageLoader.LoadImageContent(reader, new()
            {
                SchedulerGroup = group,
                Priority = READER_PRIORITY,
            });
            await TestTiming.WaitUntilAsync(() => ImageLoaderScheduler.HasBufferRequest(reader.Uri),
                "the reader should have queued its read");

            release.SetResult(null);
            await blocking.WaitAsync(Timeout);
            await FinishAsync(previewTask, readerTask);
        }
        finally
        {
            release.TrySetResult(null);
        }

        Assert.That(reads.ToArray(), Is.EqualTo(new[] { reader.Uri, preview.Uri }));
    }

    /// <summary>
    /// Occupies the only device worker of <paramref name="group"/> with a read that completes only when the
    /// returned handle is released, so that later reads queue up instead of running.
    /// </summary>
    private static async Task<(TaskCompletionSource<Stream?> Release, Task Blocking)> BlockDeviceAsync(ImageLoaderSchedulerGroup group)
    {
        TaskCompletionSource<Stream?> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ImageLoaderScheduler.Request request = new()
        {
            Group = group,
            Uri = "fake://blocker-" + Guid.NewGuid().ToString("N"),
            Sequence = ImageLoaderScheduler.NewSequence(),
        };

        Task<Stream?> blocking = ImageLoaderScheduler.AcquireBuffer(request, fingerprint: null, async () =>
        {
            started.SetResult();
            await release.Task;
            return null;
        });

        await started.Task.WaitAsync(Timeout);
        return (release, blocking);
    }

    private static async Task FinishAsync(params Task<ImageContent?>[] tasks)
    {
        foreach (Task<ImageContent?> task in tasks)
        {
            ImageContent? content = await task.WaitAsync(Timeout);
            content?.Dispose();
        }
    }

    private sealed class FakeSource : IImageSource
    {
        private static readonly byte[] sContent = [1, 2, 3, 4, 5, 6, 7, 8];

        private readonly string _uri;
        private readonly ConcurrentQueue<string> _reads;
        private readonly Task? _openGate;

        public FakeSource(string uri, ConcurrentQueue<string> reads, Task? openGate = null)
        {
            _uri = uri;
            _reads = reads;
            _openGate = openGate;
        }

        public string Uri => _uri;

        public ImageLoaderSchedulerGroup PreferredSchedulerGroup => ImageLoaderSchedulerGroup.Default;

        public ImageCacheValidationMode CacheValidationMode => ImageCacheValidationMode.Synchronized;

        public async Task<IImageConnection?> Open()
        {
            if (_openGate is not null)
            {
                await _openGate;
            }

            return new FakeConnection(this);
        }

        private sealed class FakeConnection(FakeSource owner) : IImageConnection
        {
            public string Path => owner.Uri;

            public string CacheKey => owner.Uri;

            public string Fingerprint => "fingerprint";

            public void Dispose()
            {
            }

            public Task<Stream?> OpenImageStream()
            {
                owner._reads.Enqueue(owner.Uri);
                return Task.FromResult<Stream?>(new MemoryStream(sContent, writable: false));
            }

            public IVectorImageService? OpenVectorService()
            {
                return null;
            }
        }
    }
}
