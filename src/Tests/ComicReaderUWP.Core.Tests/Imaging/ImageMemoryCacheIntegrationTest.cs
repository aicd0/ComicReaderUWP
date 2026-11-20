// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Core.Imaging;

namespace ComicReaderUWP.Core.Tests.Imaging;

/// <summary>
/// Two consumers of one page must cause one source read, even though each of them opens its own context.
/// </summary>
[TestFixture]
public class ImageMemoryCacheIntegrationTest
{
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

    private sealed class FakeSource(byte[] content, ImageLoaderSchedulerGroup? group = null, ImageCacheValidationMode validationMode = ImageCacheValidationMode.Synchronized, string uri = "fake://image") : IImageSource
    {
        private int _openCount;

        public int OpenCount => Volatile.Read(ref _openCount);

        public string Uri => uri;

        public ImageLoaderSchedulerGroup PreferredSchedulerGroup => group ?? ImageLoaderSchedulerGroup.Default;

        public ImageCacheValidationMode CacheValidationMode => validationMode;

        public Task<IImageConnection?> Open()
        {
            return Task.FromResult<IImageConnection?>(new FakeConnection(this, content));
        }

        private sealed class FakeConnection(FakeSource owner, byte[] content) : IImageConnection
        {
            public string Path => "fake://image";

            public string CacheKey => "fake-cache-key";

            public string Fingerprint => "fake-fingerprint";

            public void Dispose()
            {
            }

            public Task<Stream?> OpenImageStream()
            {
                Interlocked.Increment(ref owner._openCount);
                return Task.FromResult<Stream?>(new MemoryStream(content, writable: false));
            }

            public IVectorImageService? OpenVectorService()
            {
                return null;
            }
        }
    }

    private static ImageLoaderScheduler.Request CreateRequest(FakeSource source)
    {
        return new()
        {
            Group = ImageLoaderSchedulerGroup.Default,
            Uri = source.Uri,
            Sequence = ImageLoaderScheduler.NewSequence(),
        };
    }

    private static byte[] CreateBytes(int length)
    {
        byte[] bytes = new byte[length];
        for (int i = 0; i < length; ++i)
        {
            bytes[i] = (byte)(i * 7);
        }

        return bytes;
    }

    private static byte[] CreateBmp(int width, int height)
    {
        int rowSize = (width * 3 + 3) / 4 * 4;
        int pixelDataSize = rowSize * height;
        int fileSize = 54 + pixelDataSize;
        byte[] bytes = new byte[fileSize];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        BitConverter.GetBytes(fileSize).CopyTo(bytes, 2);
        BitConverter.GetBytes(54).CopyTo(bytes, 10);
        BitConverter.GetBytes(40).CopyTo(bytes, 14);
        BitConverter.GetBytes(width).CopyTo(bytes, 18);
        BitConverter.GetBytes(height).CopyTo(bytes, 22);
        BitConverter.GetBytes((short)1).CopyTo(bytes, 26);
        BitConverter.GetBytes((short)24).CopyTo(bytes, 28);
        BitConverter.GetBytes(pixelDataSize).CopyTo(bytes, 34);
        return bytes;
    }

    private static async Task<byte[]> ReadAndDisposeAsync(Stream? stream)
    {
        Assert.That(stream, Is.Not.Null);
        using Stream value = stream ?? throw new InvalidOperationException("Expected a stream from the image source.");
        using MemoryStream buffer = new();
        await value.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    [Test]
    public async Task TwoConsumers_ReadSourceOnce()
    {
        byte[] content = CreateBytes(256);
        FakeSource source = new(content);

        using ImageLoaderContext first = new(source);
        using ImageLoaderContext second = new(source);

        byte[] firstBytes = await ReadAndDisposeAsync(await first.GetSourceStream(CreateRequest(source)));
        byte[] secondBytes = await ReadAndDisposeAsync(await second.GetSourceStream(CreateRequest(source)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(source.OpenCount, Is.EqualTo(1));
            Assert.That(firstBytes, Is.EqualTo(content));
            Assert.That(secondBytes, Is.EqualTo(content));
        }
    }

    [Test]
    public async Task MetadataAfterPixels_ReadsResidentBytesWithoutOpening()
    {
        byte[] content = CreateBytes(256);
        FakeSource source = new(content);

        using ImageLoaderContext pixelContext = new(source);
        await ReadAndDisposeAsync(await pixelContext.GetSourceStream(CreateRequest(source)));

        using ImageLoaderContext metaContext = new(source);
        await metaContext.GetImageMeta(CreateRequest(source));

        Assert.That(source.OpenCount, Is.EqualTo(1), "buffered bytes are memory, not device work");
    }

    [Test]
    public async Task MetadataRead_AbsorbsQueuedPixelRequest()
    {
        byte[] content = CreateBytes(256);
        FakeSource source = new(content);

        // A busy device group keeps the pixel request queued, like a reader page waiting behind other work.
        var group = ImageLoaderSchedulerGroup.FromPath(@"\\device-" + Guid.NewGuid().ToString("N") + @"\share");
        TaskCompletionSource<Stream?> blocker = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Stream?> blocking = BlockDeviceAsync(group, blocker);
        Task<Stream?> pixel = ImageLoaderScheduler.AcquireBuffer(new() { Group = group, Uri = source.Uri, Sequence = ImageLoaderScheduler.NewSequence() }, fingerprint: null,
            () => Task.FromResult<Stream?>(new MemoryStream(content, writable: false)));
        Assert.That(ImageLoaderScheduler.HasBufferRequest(source.Uri), Is.True);

        using (ImageLoaderContext metaContext = new(source))
        {
            await metaContext.GetImageMeta(CreateRequest(source));
        }

        using (ImageLoaderContext pixelContext = new(source))
        {
            byte[] bytes = await ReadAndDisposeAsync(await pixelContext.GetSourceStream(CreateRequest(source)));
            Assert.That(bytes, Is.EqualTo(content));
        }

        blocker.SetResult(null);
        await blocking;
        using Stream? pixelStream = await pixel;
        Assert.That(pixelStream, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(source.OpenCount, Is.EqualTo(1), "the metadata read should serve the queued pixel request");
            Assert.That(ImageLoaderScheduler.HasBuffer(source.Uri), Is.True, "the metadata read retains the bytes it read");
        }
    }

    [Test]
    public async Task LoadImageContent_ReadsTheSourceOnce()
    {
        byte[] content = CreateBytes(256);
        FakeSource source = new(content);

        byte[] firstBytes;
        using (ImageContent? first = await ImageLoader.LoadImageContent(source, new()))
        {
            Assert.That(first, Is.Not.Null);
            ImageContent value = first ?? throw new InvalidOperationException("Expected image content.");
            using MemoryStream buffer = new();
            await value.Stream.CopyToAsync(buffer);
            firstBytes = buffer.ToArray();
            Assert.That(value.Meta, Is.Null, "these bytes are not a decodable image");
        }

        using (ImageContent? second = await ImageLoader.LoadImageContent(source, new()))
        {
            Assert.That(second, Is.Not.Null, "the second reader is served from memory");
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstBytes, Is.EqualTo(content));
            Assert.That(source.OpenCount, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task LoadImageContent_ReturnsMetaFromTheSameBytes()
    {
        byte[] content = CreateBmp(3, 2);
        FakeSource source = new(content);

        using ImageContent? image = await ImageLoader.LoadImageContent(source, new());

        Assert.That(image, Is.Not.Null);
        ImageContent value = image ?? throw new InvalidOperationException("Expected image content.");
        ImageMeta meta = value.Meta ?? throw new InvalidOperationException("Expected image meta.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(meta.Width, Is.EqualTo(3));
            Assert.That(meta.Height, Is.EqualTo(2));
            Assert.That(meta.FrameCount, Is.EqualTo(1));
            Assert.That(meta.Format, Is.EqualTo("BMP"));
            Assert.That(meta.Size, Is.EqualTo(content.Length));
            Assert.That(source.OpenCount, Is.EqualTo(1), "the meta comes from the bytes already read");
        }
    }

    [Test]
    public async Task DeferredValidation_DoesNotWaitForTheDevice()
    {
        var group = ImageLoaderSchedulerGroup.FromPath(@"\\device-" + Guid.NewGuid().ToString("N") + @"\share");
        FakeSource source = new(CreateBytes(256), group, ImageCacheValidationMode.Deferred);

        var database = new ImageCacheDatabase(Path.Combine(_directory, "Validation.sqlite"));
        ImageLoaderContext.InjectDatabase(database);
        ImageCacheDatabase.CacheRecord record = database.GetOrCreateCache("fake-cache-key");
        ImageLoaderContext.SaveImageMetaToCacheRecord(record, "fingerprint-of-another-file", new ImageMeta
        {
            Width = 1,
            Height = 1,
            DpiX = 96,
            DpiY = 96,
            Format = "png",
            BitsPerPixel = 24,
            Size = 1,
            FrameCount = 1,
        });
        record.Save();

        // The device never finishes this read, so anything that waits for it never runs.
        TaskCompletionSource<Stream?> blocker = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Stream?> blocking = BlockDeviceAsync(group, blocker);

        try
        {
            using ImageLoaderContext context = new(source);
            Assert.That(await context.GetValidationFingerprint(), Is.Null, "deferred validation has no fingerprint to offer");

            await TestTiming.WaitUntilAsync(() => record.GetExt(ImageCacheExt.IMAGE_META_VERSION) is null,
                "validation must not queue behind the device: its only I/O is a stat");
        }
        finally
        {
            blocker.SetResult(null);
            await blocking;
        }
    }

    [Test]
    public async Task OneReadThenOneHit_AreLogged()
    {
        byte[] content = CreateBytes(256);
        FakeSource source = new(content, uri: "fake://logged");
        ImagingLogCapture logs = new();
        Logger.AddListener(logs);

        try
        {
            using ImageLoaderContext first = new(source);
            await ReadAndDisposeAsync(await first.GetSourceStream(CreateRequest(source)));

            using ImageLoaderContext second = new(source);
            await ReadAndDisposeAsync(await second.GetSourceStream(CreateRequest(source)));

            await TestTiming.WaitUntilAsync(() => logs.EventsFor(source.Uri).Length == 2,
                "the read and the hit should both be logged");

            string[] events = logs.EventsFor(source.Uri);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(events[0], Does.StartWith("read [buffered] fake://logged"));
                Assert.That(events[0], Does.Contain(", cache "));
                Assert.That(events[1], Does.StartWith("hit [resident] fake://logged"));
            }
        }
        finally
        {
            Logger.RemoveListener(logs);
        }
    }

    [Test]
    public async Task CancelledMetadataRequest_DoesNotReadTheSource()
    {
        FakeSource source = new(CreateBmp(4, 4));
        CancellationSession cancellation = new();
        CancellationSession.IToken token = cancellation.Token;
        cancellation.Next();

        using ImageLoaderContext context = new(source);
        ImageMeta? meta = await context.GetImageMeta(CreateRequest(source) with { Token = token });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(meta, Is.Null);
            Assert.That(source.OpenCount, Is.Zero, "a cancelled request must not open the source");
        }
    }

    [Test]
    public async Task MetadataThenPixels_ReadsTheSourceOnce()
    {
        byte[] content = CreateBmp(4, 4);
        FakeSource source = new(content);

        using (ImageLoaderContext metaContext = new(source))
        {
            ImageMeta? meta = await metaContext.GetImageMeta(CreateRequest(source));
            Assert.That(meta, Is.Not.Null, "the bytes are a decodable image");
        }

        using (ImageLoaderContext pixelContext = new(source))
        {
            byte[] bytes = await ReadAndDisposeAsync(await pixelContext.GetSourceStream(CreateRequest(source)));
            Assert.That(bytes, Is.EqualTo(content));
        }

        Assert.That(source.OpenCount, Is.EqualTo(1), "the header read keeps the bytes for the pixels that follow");
    }

    [Test]
    public async Task MetadataOnly_RetainsTheBytesItRead()
    {
        byte[] content = CreateBytes(256);
        FakeSource source = new(content);

        using ImageLoaderContext context = new(source);
        ImageMeta? meta = await context.GetImageMeta(CreateRequest(source));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(source.OpenCount, Is.EqualTo(1), "the header comes from one read of the source");
            Assert.That(meta, Is.Null, "these bytes are not a decodable image");
            Assert.That(ImageLoaderScheduler.HasBuffer(source.Uri), Is.True, "the bytes stay for the pixels that follow");
        }
    }

    /// <summary>Occupies the device of <paramref name="group"/> with a read that finishes when released.</summary>
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
}
