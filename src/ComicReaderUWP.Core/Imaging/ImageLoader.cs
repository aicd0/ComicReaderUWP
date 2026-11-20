// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Drawing;
using System.Text;

using ComicReaderUWP.Core.Common.Caching;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Storage;
using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Core.Common.Utils;

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.Graphics.Imaging;

namespace ComicReaderUWP.Core.Imaging;

public static partial class ImageLoader
{
    private const string TAG = nameof(ImageLoader);
    private const int VERSION = 1;
    private const string IMAGES_FOLDER_NAME = "Thumbnails";
    private const string MAIN_DATABASE_FILE_NAME = "Images.sqlite";
    private const long MIN_CACHE_CAPACITY = 1024 * 1024 * 1024;
    private const int CACHE_TTL_DAYS = 30;
    private const int MAX_CACHE_ROWS = 100000;

    private static string _cacheDirectoryPath = string.Empty;
    private static ImageCacheDatabase? sImageCacheDatabase;
    private static readonly Lock sLock = new();
    private static volatile LRUCache? sImageCache;

    private static int sPostMainThreadTask = 0;
    private static readonly ConcurrentQueue<DecodingImageItem> sDecodeQueue = new();

    private static ImageCacheDatabase Database => sImageCacheDatabase ?? throw new InvalidOperationException("ImageCacheManager not initialized");

    public static void Initialize(string cacheDirectoryPath, bool clear)
    {
        _cacheDirectoryPath = cacheDirectoryPath;

        string versionFilePath = Path.Combine(_cacheDirectoryPath, "Version.txt");
        int version = clear ? -1 : ReadVersion(versionFilePath);
        if (version != VERSION)
        {
            switch (version)
            {
                case -1:
                    DeleteDirectory(Path.Combine(StorageLocation.LocalCacheFolderPath, IMAGES_FOLDER_NAME), throwOnError: false);
                    DeleteDirectory(_cacheDirectoryPath);
                    break;
                default:
                    break;
            }
        }

        if (!Directory.Exists(_cacheDirectoryPath))
        {
            Directory.CreateDirectory(_cacheDirectoryPath);
        }

        if (version != VERSION)
        {
            File.WriteAllText(versionFilePath, VERSION.ToString());
        }

        string databaseFilePath = Path.Combine(_cacheDirectoryPath, MAIN_DATABASE_FILE_NAME);
        ImageCacheDatabase database = new(databaseFilePath);
        sImageCacheDatabase = database;
        ImageLoaderContext.InjectDatabase(database);

        TaskDispatcher.LongRunningThreadPool.Submit(() =>
        {
            database.Cleanup(TimeSpan.FromDays(CACHE_TTL_DAYS), MAX_CACHE_ROWS);
        });
    }

    public static void Clear()
    {
        Database.Clear();
        sImageCache?.Clear();
        ImageLoaderScheduler.ClearBuffer();
    }

    public static void SetMemoryCacheCapacity(long bytes)
    {
        ImageLoaderScheduler.SetBufferCapacity(bytes);
    }

    public static Task LoadImage(IImageSource source, LoadImageOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        options = options.Clone();
        ImageLoaderScheduler.Request request = new()
        {
            Group = options.SchedulerGroup ?? source.PreferredSchedulerGroup,
            Priority = options.Priority,
            Uri = source.Uri,
            Sequence = ImageLoaderScheduler.NewSequence(),
            Token = options.Token,
        };
        return TaskDispatcher.DefaultThreadPool.SubmitAsync(async () =>
        {
            using ImageLoaderContext context = new(source);
            if (!await LoadImageCore(context, options, request))
            {
                CoroutineUtils.RunInMainThread(options.Handler.OnFailure);
            }
        });
    }

    public static Task LoadImage(ImageLoaderContext loaderContext, LoadImageOptions options)
    {
        ArgumentNullException.ThrowIfNull(loaderContext);
        ArgumentNullException.ThrowIfNull(options);

        options = options.Clone();
        ImageLoaderScheduler.Request request = new()
        {
            Group = options.SchedulerGroup ?? loaderContext.Source.PreferredSchedulerGroup,
            Priority = options.Priority,
            Uri = loaderContext.Source.Uri,
            Sequence = ImageLoaderScheduler.NewSequence(),
            Token = options.Token,
        };
        return TaskDispatcher.DefaultThreadPool.SubmitAsync(async () =>
        {
            if (!await LoadImageCore(loaderContext, options, request))
            {
                CoroutineUtils.RunInMainThread(options.Handler.OnFailure);
            }
        });
    }

    public static Task<ImageContent?> LoadImageContent(IImageSource source, LoadImageContentOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        options = options.Clone();
        ImageLoaderScheduler.Request request = new()
        {
            Group = options.SchedulerGroup ?? source.PreferredSchedulerGroup,
            Priority = options.Priority,
            Uri = source.Uri,
            Sequence = ImageLoaderScheduler.NewSequence(),
            Token = options.Token,
        };
        return TaskDispatcher.DefaultThreadPool.SubmitAsync(async () =>
        {
            using ImageLoaderContext context = new(source);
            return await LoadImageContentCore(context, request);
        });
    }

    public static Task<ImageMeta?> LoadImageMeta(IImageSource source, LoadImageMetaOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        options = options.Clone();
        ImageLoaderScheduler.Request request = new()
        {
            Group = options.SchedulerGroup ?? source.PreferredSchedulerGroup,
            Priority = options.Priority,
            Uri = source.Uri,
            Sequence = ImageLoaderScheduler.NewSequence(),
            Token = options.Token,
        };
        return TaskDispatcher.DefaultThreadPool.SubmitAsync(async () =>
        {
            using ImageLoaderContext context = new(source);
            return await GetImageMeta(context, request);
        });
    }

    public static Task<ImageMeta?> LoadImageMeta(ImageLoaderContext loaderContext, LoadImageMetaOptions options)
    {
        ArgumentNullException.ThrowIfNull(loaderContext);
        ArgumentNullException.ThrowIfNull(options);

        options = options.Clone();
        ImageLoaderScheduler.Request request = new()
        {
            Group = options.SchedulerGroup ?? loaderContext.Source.PreferredSchedulerGroup,
            Priority = options.Priority,
            Uri = loaderContext.Source.Uri,
            Sequence = ImageLoaderScheduler.NewSequence(),
            Token = options.Token,
        };
        return TaskDispatcher.DefaultThreadPool.SubmitAsync(
            () => GetImageMeta(loaderContext, request));
    }

    private static async Task<ImageContent?> LoadImageContentCore(ImageLoaderContext context, ImageLoaderScheduler.Request request)
    {
        Stream? stream = await context.GetSourceStream(request);
        if (stream is null)
        {
            return null;
        }

        ImageMeta? meta = await GetImageMeta(context, request);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return new ImageContent(stream, meta);
    }

    private static async Task<ImageMeta?> GetImageMeta(ImageLoaderContext context, ImageLoaderScheduler.Request request)
    {
        ImageCacheDatabase.CacheRecord? record = await context.GetOrCreateCache();
        if (record is null)
        {
            return null;
        }

        return await record.Enqueue(async () =>
        {
            string? fingerprint = await context.GetValidationFingerprint();
            ImageMeta? meta = CreateImageMetaFromCacheRecord(record, fingerprint);
            if (meta is not null)
            {
                return meta;
            }

            meta = await context.GetImageMeta(request);
            if (meta is null)
            {
                return null;
            }

            fingerprint ??= await context.GetFingerprint();
            record.Clear();
            ImageLoaderContext.SaveImageMetaToCacheRecord(record, fingerprint, meta);
            record.Save();

            meta = CreateImageMetaFromCacheRecord(record, fingerprint);
            if (meta is null)
            {
                Logger.F(TAG, "Failed to get image meta from cache record after saving");
                return null;
            }

            return meta;
        });
    }

    private static ImageMeta? CreateImageMetaFromCacheRecord(ImageCacheDatabase.CacheRecord record, string? fingerprint)
    {
        if (!ImageLoaderContext.ValidateCacheRecord(record, fingerprint))
        {
            return null;
        }

        bool TryReadStringExt(string key, out string value)
        {
            string? valueString = record.GetExt(key);
            if (valueString is not null)
            {
                value = valueString;
                return true;
            }

            value = string.Empty;
            return false;
        }

        bool TryReadIntExt(string key, out int value)
        {
            string? valueString = record.GetExt(key);
            if (!string.IsNullOrEmpty(valueString) && int.TryParse(valueString, out value))
            {
                return true;
            }

            value = 0;
            return false;
        }

        bool TryReadLongExt(string key, out long value)
        {
            string? valueString = record.GetExt(key);
            if (!string.IsNullOrEmpty(valueString) && long.TryParse(valueString, out value))
            {
                return true;
            }

            value = 0;
            return false;
        }

        bool TryReadDoubleExt(string key, out double value)
        {
            string? valueString = record.GetExt(key);
            if (!string.IsNullOrEmpty(valueString) && double.TryParse(valueString, out value))
            {
                return true;
            }

            value = 0;
            return false;
        }

        if (!TryReadIntExt(ImageCacheExt.IMAGE_META_WIDTH, out int width))
        {
            return null;
        }

        if (!TryReadIntExt(ImageCacheExt.IMAGE_META_HEIGHT, out int height))
        {
            return null;
        }

        if (!TryReadDoubleExt(ImageCacheExt.IMAGE_META_DPI_X, out double dpiX))
        {
            return null;
        }

        if (!TryReadDoubleExt(ImageCacheExt.IMAGE_META_DPI_Y, out double dpiY))
        {
            return null;
        }

        if (!TryReadIntExt(ImageCacheExt.IMAGE_META_BITS_PER_PIXEL, out int bitsPerPixel))
        {
            return null;
        }

        if (!TryReadLongExt(ImageCacheExt.IMAGE_META_SIZE, out long size))
        {
            return null;
        }

        if (!TryReadStringExt(ImageCacheExt.IMAGE_META_DECODER_NAME, out string decoderName))
        {
            return null;
        }

        if (!TryReadIntExt(ImageCacheExt.IMAGE_META_FRAME_COUNT, out int frameCount))
        {
            return null;
        }

        return new ImageMeta
        {
            Width = width,
            Height = height,
            DpiX = dpiX,
            DpiY = dpiY,
            Format = decoderName,
            BitsPerPixel = bitsPerPixel,
            Size = size,
            FrameCount = frameCount
        };
    }

    private static async Task<bool> LoadImageCore(ImageLoaderContext context, LoadImageOptions options, ImageLoaderScheduler.Request request)
    {
        if (request.IsCancellationRequested)
        {
            return false;
        }

        long startTime = GetCurrentTick();

        ImageMeta? meta = await GetImageMeta(context, request);
        if (meta is null || request.IsCancellationRequested)
        {
            return false;
        }

        Size originalSize = new(meta.Width, meta.Height);
        CalculateDesiredDimension(
            options.DecodeWidth,
            options.DecodeHeight,
            originalSize.Width,
            originalSize.Height,
            out bool useOriginalSize,
            out Size desiredSize);

        // Schedule ImageSource creation
        Func<Task<DecodedImageModel>> createFunc;
        Action cleanupAction;
        if (useOriginalSize)
        {
            IVectorImageService? vectorService = await context.GetVectorService();
            if (vectorService is not null)
            {
                CalculateDefaultSizeForVector(originalSize.Width, originalSize.Height, out int width, out int height);
                SoftwareBitmap? softwareBitmap = await vectorService.CreateSoftwareBitmap(width, height);

                if (softwareBitmap is null)
                {
                    return false;
                }

                createFunc = async () =>
                {
                    SoftwareBitmapSource source = new();
                    await source.SetBitmapAsync(softwareBitmap);
                    return new() { Source = source };
                };
                cleanupAction = softwareBitmap.Dispose;
            }
            else
            {
                Stream? stream = await context.GetSourceStream(request);
                if (stream is null)
                {
                    return false;
                }

                createFunc = async () =>
                {
                    BitmapImage bitmap = new();
                    await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
                    return new() { Source = bitmap };
                };
                cleanupAction = stream.Dispose;
            }
        }
        else
        {
            LRUCache? imageCache = GetImageLRUCache();
            if (imageCache is null)
            {
                Logger.F(TAG, "Image cache is null");
                return false;
            }

            ImageCacheDatabase.CacheRecord? record = await context.GetOrCreateCache();
            if (record is null)
            {
                Logger.F(TAG, "Cache record is null");
                return false;
            }

            string? fingerprint = await context.GetValidationFingerprint();

            Tuple<Func<Task<DecodedImageModel>>, Action>? tuple = await record.Enqueue<Tuple<Func<Task<DecodedImageModel>>, Action>?>(async () =>
            {
                Func<Task<DecodedImageModel>> createFunc;
                Action cleanupAction;
                Stream? thumbnailStream = null;
                try
                {
                    thumbnailStream = OpenThumbnailStreamFromCacheRecord(imageCache, record, fingerprint, originalSize, desiredSize, out List<string> cacheEntryKeys);
                    if (thumbnailStream is not null)
                    {
                        Stream stream = thumbnailStream;
                        thumbnailStream = null;
                        createFunc = async () =>
                        {
                            var bitmap = new BitmapImage
                            {
                                DecodePixelWidth = desiredSize.Width,
                                DecodePixelHeight = desiredSize.Height,
                                DecodePixelType = DecodePixelType.Physical,
                            };
                            await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
                            return new() { Source = bitmap };
                        };
                        cleanupAction = stream.Dispose;
                    }
                    else
                    {
                        if (cacheEntryKeys.Count > 0)
                        {
                            string cacheEntryKey = cacheEntryKeys[0];
                            fingerprint ??= await context.GetFingerprint();
                            await SaveThumbnail(context, request, cacheEntryKey, fingerprint, originalSize, imageCache, record);
                        }

                        IVectorImageService? vectorService = await context.GetVectorService();
                        if (vectorService is not null)
                        {
                            var device = CanvasDevice.GetSharedDevice();
                            CanvasBitmap? canvasBitmap = await vectorService.CreateImageCanvasBitmap(device, desiredSize.Width, desiredSize.Height);

                            if (canvasBitmap is null)
                            {
                                return null;
                            }

                            createFunc = async () =>
                            {
                                var source = new CanvasImageSource(device, desiredSize.Width, desiredSize.Height, 96F);
                                using CanvasDrawingSession ds = source.CreateDrawingSession(Colors.Black);
                                ds.DrawImage(canvasBitmap);
                                return new() { Source = source };
                            };
                            cleanupAction = canvasBitmap.Dispose;
                        }
                        else
                        {
                            Stream? stream = await context.GetSourceStream(request);
                            if (stream is null)
                            {
                                return null;
                            }

                            createFunc = async () =>
                            {
                                var bitmap = new BitmapImage
                                {
                                    DecodePixelWidth = desiredSize.Width,
                                    DecodePixelHeight = desiredSize.Height,
                                    DecodePixelType = DecodePixelType.Physical,
                                };
                                await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
                                return new() { Source = bitmap };
                            };
                            cleanupAction = stream.Dispose;
                        }
                    }

                    return new(createFunc, cleanupAction);
                }
                finally
                {
                    thumbnailStream?.Dispose();
                    thumbnailStream = null;
                }
            });

            if (tuple is null)
            {
                return false;
            }

            createFunc = tuple.Item1;
            cleanupAction = tuple.Item2;
        }

        DecodingImageItem decodingItem = new()
        {
            CreateFunc = createFunc,
            CleanupAction = cleanupAction,
            Options = options,
            StartTime = startTime,
        };

        sDecodeQueue.Enqueue(decodingItem);
        ScheduleDecoding();
        return true;
    }

    private static void CalculateDesiredDimension(double frameWidth, double frameHeight,
        double originWidth, double originHeight,
        out bool useOriginalSize, out Size desiredSize)
    {
        double desiredWidthRaw = Math.Min(frameWidth, originWidth);
        double desiredHeightRaw = Math.Min(frameHeight, originHeight);
        useOriginalSize = desiredWidthRaw >= originWidth && desiredHeightRaw >= originHeight;
        int desiredWidth = Math.Max(1, (int)Math.Round(desiredWidthRaw));
        int desiredHeight = Math.Max(1, (int)Math.Round(desiredHeightRaw));
        desiredSize = new(desiredWidth, desiredHeight);
    }

    private static void CalculateDefaultSizeForVector(float originalWidth, float originalHeight, out int width, out int height)
    {
        int defaultWidth = 764;
        int defaultHeight = 1080;

        if (!(float.IsFinite(originalWidth) && float.IsFinite(originalHeight) && originalWidth > 0 && originalHeight > 0))
        {
            width = defaultWidth;
            height = defaultHeight;
            return;
        }

        int screenWidth = 1920;
        int screenHeight = 1080;

        float pageAspectRatio = originalWidth / originalHeight;
        float screenAspectRatio = (float)screenWidth / screenHeight;

        float targetWidth, targetHeight;
        if (pageAspectRatio > screenAspectRatio)
        {
            targetHeight = screenHeight;
            targetWidth = screenHeight / originalHeight * originalWidth;
        }
        else
        {
            targetWidth = screenWidth;
            targetHeight = screenWidth / originalWidth * originalHeight;
        }

        float maxResolution = 10000000;
        float targetResolution = targetWidth * targetHeight;
        if (targetResolution > maxResolution)
        {
            float dimensionFactor = (float)Math.Sqrt(maxResolution / targetResolution);
            targetWidth *= dimensionFactor;
            targetHeight *= dimensionFactor;
        }
        width = (int)targetWidth;
        height = (int)targetHeight;
    }

    private static Stream? OpenThumbnailStreamFromCacheRecord(LRUCache imageCache, ImageCacheDatabase.CacheRecord record, string? fingerprint,
        SizeF originalSize, Size desiredSize, out List<string> cacheEntryKeys)
    {
        cacheEntryKeys = ImageCacheStrategy.CalculateCacheEntryKeys(desiredSize.Width, desiredSize.Height, originalSize);

        if (!ImageLoaderContext.ValidateCacheRecord(record, fingerprint))
        {
            return null;
        }

        foreach (string cacheEntryKey in cacheEntryKeys)
        {
            string? entry = record.GetCacheEntry(cacheEntryKey);
            if (!string.IsNullOrEmpty(entry))
            {
                Stream? thumbnailStream = imageCache.Get(entry);
                if (thumbnailStream != null)
                {
                    return thumbnailStream;
                }
            }
        }

        return null;
    }

    private static async Task SaveThumbnail(ImageLoaderContext context, ImageLoaderScheduler.Request request, string cacheEntryKey, string fingerprint,
        Size originalSize, LRUCache imageCache, ImageCacheDatabase.CacheRecord record)
    {
        if (!CalculateDesiredThumbnailSize(cacheEntryKey, originalSize, out Size thumbnailSize))
        {
            return;
        }

        SoftwareBitmap? thumbnailBitmap;
        IVectorImageService? vectorService = await context.GetVectorService();
        if (vectorService is not null)
        {
            thumbnailBitmap = await vectorService.CreateSoftwareBitmap(thumbnailSize.Width, thumbnailSize.Height);
        }
        else
        {
            Stream? content = await context.GetSourceStream(request);
            if (content is null)
            {
                return;
            }

            using (content)
            {
                BitmapDecoder decoder;
                try
                {
                    decoder = await BitmapDecoder.CreateAsync(content.AsRandomAccessStream());
                }
                catch (Exception ex)
                {
                    Logger.E(TAG, ex);
                    return;
                }

                thumbnailBitmap = await CreateScaledSoftwareBitmap(decoder, thumbnailSize.Width, thumbnailSize.Height);
            }
        }

        if (thumbnailBitmap is null)
        {
            return;
        }

        try
        {
            string entry = RandomFileName(16);
            using LRUCacheStream? cacheFileStream = imageCache.Put(entry);
            if (cacheFileStream is null)
            {
                return;
            }

            try
            {
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, cacheFileStream.AsRandomAccessStream());
                encoder.SetSoftwareBitmap(thumbnailBitmap);
                encoder.IsThumbnailGenerated = false;
                await encoder.FlushAsync();
            }
            catch (Exception ex)
            {
                Logger.F(TAG, "TryCreateImageCache", ex);
                return;
            }

            record.PutCacheEntry(cacheEntryKey, entry);
            record.PutExt(ImageCacheExt.IMAGE_META_FINGERPRINT, fingerprint);
            record.Save();
        }
        finally
        {
            thumbnailBitmap?.Dispose();
        }
    }

    private static async Task<SoftwareBitmap?> CreateScaledSoftwareBitmap(BitmapDecoder decoder, int scaledWidth, int scaledHeight)
    {
        BitmapTransform transform = new()
        {
            ScaledWidth = (uint)scaledWidth,
            ScaledHeight = (uint)scaledHeight,
            InterpolationMode = BitmapInterpolationMode.Fant,
        };

        SoftwareBitmap? softwareBitmap = null;
        try
        {
            softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.ColorManageToSRgb);
        }
        catch (Exception ex)
        {
            Logger.E(TAG, ex);
        }

        return softwareBitmap;
    }

    private static bool CalculateDesiredThumbnailSize(string cacheEntryKey, Size originalSize, out Size thumbnailSize)
    {
        thumbnailSize = new();

        int cacheResolution = ImageCacheStrategy.GetCacheResolution(cacheEntryKey);
        if (cacheResolution <= 0)
        {
            return false;
        }

        double sourceResolution = originalSize.Width * originalSize.Height;
        if (sourceResolution <= cacheResolution)
        {
            thumbnailSize = originalSize;
            return true;
        }

        double scaleRatio = cacheResolution / sourceResolution;
        double dimensionRatio = Math.Sqrt(scaleRatio);
        int aspectHeight = (int)Math.Floor(originalSize.Height * dimensionRatio);
        int aspectWidth = (int)Math.Floor(originalSize.Width * dimensionRatio);
        thumbnailSize = new(aspectWidth, aspectHeight);
        return true;
    }

    private static void ScheduleDecoding()
    {
        if (Interlocked.CompareExchange(ref sPostMainThreadTask, 1, 0) == 1)
        {
            return;
        }

        CoroutineUtils.PostInMainThreadAsync(async () =>
        {
            long startTime = GetCurrentTick();
            Interlocked.Exchange(ref sPostMainThreadTask, 0);
            // Only responsible for rendering tasks that have been queued before this point
            while (sDecodeQueue.TryDequeue(out DecodingImageItem? item))
            {
                DecodedImageModel? result;
                try
                {
                    result = await PerformDecoding(item);
                }
                finally
                {
                    item.CleanupAction();
                }

                if (result is not null)
                {
                    item.Options.Handler.OnSuccess(result);
                }
                else
                {
                    item.Options.Handler.OnFailure();
                }

                // Keep main thread responsive
                if (GetCurrentTick() - startTime > 50)
                {
                    if (sDecodeQueue.TryPeek(out _))
                    {
                        ScheduleDecoding();
                    }

                    break;
                }
            }
        }, DispatcherQueuePriority.Low);
    }

    private static async Task<DecodedImageModel?> PerformDecoding(DecodingImageItem item)
    {
        if (item.Options.Token.IsCancellationRequested)
        {
            return null;
        }

        DecodedImageModel result;
        try
        {
            result = await item.CreateFunc();
        }
        catch (Exception ex)
        {
            Logger.F(TAG, ex);
            return null;
        }

        if (item.Options.Token.IsCancellationRequested)
        {
            return null;
        }

        return result;
    }

    private static LRUCache? GetImageLRUCache()
    {
        LRUCache? cache = sImageCache;
        if (cache is not null)
        {
            return cache;
        }

        string folderPath = Path.Combine(_cacheDirectoryPath, IMAGES_FOLDER_NAME);

        lock (sLock)
        {
            cache = sImageCache;
            if (cache is not null)
            {
                return cache;
            }

            try
            {
                Directory.CreateDirectory(folderPath);
            }
            catch (Exception ex)
            {
                Logger.F(TAG, ex);
                return null;
            }

            cache = new(folderPath);
            sImageCache = cache;
        }

        TaskDispatcher.LongRunningThreadPool.Submit(() =>
        {
            long cacheSize = cache.GetApproximateSize();
            long freeSpace = GetFreeSpace(folderPath) + cacheSize;
            long cacheCapacity = Math.Max(freeSpace / 10, MIN_CACHE_CAPACITY); // Use up to 10% of free space, but at least MIN_CACHE_CAPACITY
            if (cacheSize > cacheCapacity)
            {
                cache.Cleanup(cacheCapacity);
            }
        });

        Logger.I(TAG, $"Image cache initialized");
        return cache;
    }

    private static long GetFreeSpace(string path)
    {
        string? root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root))
        {
            Logger.F(TAG, $"Failed to get root from path: {path}");
            return 0;
        }

        var drive = new DriveInfo(root);
        if (!drive.IsReady)
        {
            Logger.F(TAG, $"Drive is not ready: {root}");
            return 0;
        }

        try
        {
            return drive.AvailableFreeSpace;
        }
        catch (Exception ex)
        {
            Logger.F(TAG, $"Failed to access drive info for: {root}", ex);
            return 0;
        }
    }

    private static int ReadVersion(string versionFilePath)
    {
        if (!File.Exists(versionFilePath))
        {
            return -1;
        }

        string versionContent;
        try
        {
            versionContent = File.ReadAllText(versionFilePath);
        }
        catch (Exception ex)
        {
            Logger.E(TAG, ex);
            return -1;
        }

        if (int.TryParse(versionContent, out int version))
        {
            return version;
        }

        return -1;
    }

    private static void DeleteDirectory(string path, bool throwOnError = true)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, true);
        }
        catch (Exception ex)
        {
            if (throwOnError)
            {
                throw;
            }

            Logger.E(TAG, $"Failed to delete directory: {path}", ex);
        }
    }

    private static string RandomFileName(int length)
    {
        const string symbols = "0123456789abcdefghijklmnopqrstuvwxyz";
        StringBuilder sb = new();
        for (int i = 0; i < length; ++i)
        {
            sb.Append(symbols[Random.Shared.Next(symbols.Length)]);
        }

        return sb.ToString();
    }

    private static long GetCurrentTick()
    {
        return Environment.TickCount64;
    }

    private class DecodingImageItem
    {
        public required Func<Task<DecodedImageModel>> CreateFunc;
        public required Action CleanupAction;
        public required LoadImageOptions Options;
        public long StartTime;
    }
}
