// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Utils;
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

namespace ComicReaderUWP.Common.Imaging;

internal static partial class ImageCacheManager
{
    private const string TAG = "ImageCacheManager";
    private const int VERSION = 1;
    private const int IMAGE_META_VERSION = 2;
    private const string IMAGES_FOLDER_NAME = "images";
    private const string MAIN_DATABASE_FILE_NAME = "db_main.db";
    private const long MIN_CACHE_CAPACITY = 1024 * 1024 * 1024;

    private static string _cacheDirectoryPath = string.Empty;
    private static ImageCacheDatabase? sImageCacheDatabase;
    private static readonly object sLock = new();
    private static volatile LRUCache? sImageCache;

    private static int sPostMainThreadTask = 0;
    private static readonly ConcurrentQueue<DecodingImageItem> sDecodeQueue = new();

    private static ImageCacheDatabase ImageCacheDatabase => sImageCacheDatabase ?? throw new InvalidOperationException("ImageCacheManager not initialized");

    public static void Initialize(string cacheDirectoryPath, bool clear)
    {
        _cacheDirectoryPath = cacheDirectoryPath;

        string versionFilePath = Path.Combine(_cacheDirectoryPath, "version.txt");
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
        sImageCacheDatabase = new(databaseFilePath);
    }

    public static void Clear()
    {
        ImageCacheDatabase.Clear();
        sImageCache?.Clear();
    }

    public static bool TryGetOriginalDimension(IImageSource source, out SizeF dimension)
    {
        if (MainThreadUtils.IsMainThread())
        {
            Logger.F(TAG, "GetOriginalDimension cannot be called on main thread");
            dimension = new();
            return false;
        }

        using CacheRequestContext context = new(source);
        return TryGetOriginalDimension(context, out dimension);
    }

    public static ImageMeta? GetImageMeta(IImageSource source)
    {
        if (MainThreadUtils.IsMainThread())
        {
            Logger.F(TAG, "GetImageMeta cannot be called on main thread");
            return null;
        }

        using CacheRequestContext context = new(source);
        return GetImageMeta(context);
    }

    public static void LoadImage(IImageSource source, LoadImageOptions options)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));
        ArgumentNullException.ThrowIfNull(options, nameof(options));

        if (MainThreadUtils.IsMainThread())
        {
            Logger.F(TAG, "LoadImage cannot be called on main thread");
            options.Handler.OnFailure();
            return;
        }

        options = options.Clone();
        options.FrameWidth *= DisplayUtils.GetRawPixelPerPixel() * 1.2;
        options.FrameHeight *= DisplayUtils.GetRawPixelPerPixel() * 1.2;

        using CacheRequestContext context = new(source);
        if (!LoadImage(context, options))
        {
            CoroutineUtils.RunInMainThread(options.Handler.OnFailure);
        }
    }

    private static bool TryGetOriginalDimension(CacheRequestContext context, out SizeF dimension)
    {
        IVectorImageService? vectorService = context.GetVectorService();
        if (vectorService is not null)
        {
            dimension = vectorService.Size;
            return true;
        }

        ImageMeta? meta = GetImageMeta(context);
        if (meta is not null)
        {
            dimension = new(meta.Width, meta.Height);
            return true;
        }

        dimension = new();
        return false;
    }

    private static ImageMeta? GetImageMeta(CacheRequestContext context)
    {
        ImageCacheDatabase.CacheRecord? record = ImageCacheDatabase.GetOrCreate(context.Source.Uri);
        if (record is null)
        {
            return null;
        }

        record.Lock.AcquireReaderLock(Timeout.Infinite);
        try
        {
            string? fingerprint = context.Source.ValidateFingerprint ? context.GetFingerprint() : null;
            ImageMeta? meta = CreateImageMetaFromCacheRecord(record, fingerprint);
            if (meta is not null)
            {
                return meta;
            }

            LockCookie lockCookie = record.Lock.UpgradeToWriterLock(Timeout.Infinite);
            try
            {
                meta = CreateImageMetaFromCacheRecord(record, fingerprint);
                if (meta is not null)
                {
                    return meta;
                }

                Stream? stream = context.GetSourceStream();
                if (stream is null)
                {
                    return null;
                }

                BitmapDecoder? decoder = context.GetBitmapDecoder();
                if (decoder is null)
                {
                    return null;
                }

                fingerprint ??= context.GetFingerprint();
                record.Clear();
                SaveImageMetaToCacheRecord(record, fingerprint, stream.Length, decoder);
                record.Save();

                ImageMeta? imageMeta = CreateImageMetaFromCacheRecord(record, fingerprint);
                if (imageMeta is null)
                {
                    Logger.F(TAG, "Failed to get image meta from cache record after saving");
                    return null;
                }

                return imageMeta;
            }
            finally
            {
                record.Lock.DowngradeFromWriterLock(ref lockCookie);
            }
        }
        finally
        {
            record.Lock.ReleaseReaderLock();
        }
    }

    private static ImageMeta? CreateImageMetaFromCacheRecord(ImageCacheDatabase.CacheRecord record, string? fingerprint)
    {
        if (!ValidateCacheRecord(record, fingerprint))
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

    private static void SaveImageMetaToCacheRecord(ImageCacheDatabase.CacheRecord record, string sourceFingerprint, long size, BitmapDecoder decoder)
    {
        uint width = decoder.OrientedPixelWidth;
        uint height = decoder.OrientedPixelHeight;
        if (width == 0 || height == 0)
        {
            Logger.F(TAG, $"Invalid image dimensions: width={width}, height={height}");
            return;
        }

        int bitsPerPixel = decoder.BitmapPixelFormat switch
        {
            BitmapPixelFormat.Unknown => 0,
            BitmapPixelFormat.Rgba16 => 64,
            BitmapPixelFormat.Rgba8 => 32,
            BitmapPixelFormat.Gray16 => 16,
            BitmapPixelFormat.Gray8 => 8,
            BitmapPixelFormat.Bgra8 => 32,
            BitmapPixelFormat.Nv12 => 12,
            BitmapPixelFormat.P010 => 24,
            BitmapPixelFormat.Yuy2 => 16,
            _ => 0,
        };
        string decoderName = decoder.DecoderInformation.FriendlyName switch
        {
            "JPEG Decoder" => "JPEG",
            "PNG Decoder" => "PNG",
            "BMP Decoder" => "BMP",
            "GIF Decoder" => "GIF",
            "TIFF Decoder" => "TIFF",
            "Microsoft Webp Decoder" => "WebP",
            _ => decoder.DecoderInformation.FriendlyName,
        };

        record.PutExt(ImageCacheExt.IMAGE_META_VERSION, IMAGE_META_VERSION.ToString());
        record.PutExt(ImageCacheExt.IMAGE_META_FINGERPRINT, sourceFingerprint);
        record.PutExt(ImageCacheExt.IMAGE_META_WIDTH, width.ToString());
        record.PutExt(ImageCacheExt.IMAGE_META_HEIGHT, height.ToString());
        record.PutExt(ImageCacheExt.IMAGE_META_DPI_X, decoder.DpiX.ToString());
        record.PutExt(ImageCacheExt.IMAGE_META_DPI_Y, decoder.DpiY.ToString());
        record.PutExt(ImageCacheExt.IMAGE_META_BITS_PER_PIXEL, bitsPerPixel.ToString());
        record.PutExt(ImageCacheExt.IMAGE_META_DECODER_NAME, decoderName);
        record.PutExt(ImageCacheExt.IMAGE_META_SIZE, size.ToString());
        record.PutExt(ImageCacheExt.IMAGE_META_FRAME_COUNT, decoder.FrameCount.ToString());
    }

    private static bool LoadImage(CacheRequestContext context, LoadImageOptions options)
    {
        if (options.Token.IsCancellationRequested)
        {
            return false;
        }

        long startTime = GetCurrentTick();
        string uri = context.Source.Uri;
        if (string.IsNullOrEmpty(uri))
        {
            Logger.E(TAG, "Image source URI is null or empty");
            return false;
        }

        LRUCache? imageCache = GetImageLRUCache();
        if (imageCache is null)
        {
            Logger.F(TAG, "Image cache is null");
            return false;
        }

        if (!TryGetOriginalDimension(context, out SizeF originalSize))
        {
            return false;
        }

        CalculateDesiredDimension(
            options.FrameWidth,
            options.FrameHeight,
            options.StretchMode,
            originalSize.Width,
            originalSize.Height,
            out bool useOriginalSize,
            out Size desiredSize);

        // Schedule ImageSource creation
        Func<Task<DecodedImageModel>> createFunc;
        Action cleanupAction;
        if (useOriginalSize)
        {
            IVectorImageService? vectorService = context.GetVectorService();
            if (vectorService is not null)
            {
                CalculateDefaultSizeForVector(originalSize.Width, originalSize.Height, out int width, out int height);
                SoftwareBitmap? softwareBitmap = vectorService.CreateSoftwareBitmap(width, height);

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
                Stream? stream = context.GetSourceStream();
                context.UnrefSourceStream();

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
            ImageCacheDatabase.CacheRecord? record = ImageCacheDatabase.GetOrCreate(uri);
            if (record is null)
            {
                Logger.F(TAG, "Cache record is null");
                return false;
            }

            string? fingerprint = context.Source.ValidateFingerprint ? context.GetFingerprint() : null;

            Stream? thumbnailStream = null;
            record.Lock.AcquireReaderLock(Timeout.Infinite);
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
                        fingerprint ??= context.GetFingerprint();
                        LockCookie lockCookie = record.Lock.UpgradeToWriterLock(Timeout.Infinite);
                        try
                        {
                            SaveThumbnail(context, cacheEntryKey, fingerprint, originalSize, imageCache, record);
                        }
                        finally
                        {
                            record.Lock.DowngradeFromWriterLock(ref lockCookie);
                        }
                    }

                    IVectorImageService? vectorService = context.GetVectorService();
                    if (vectorService is not null)
                    {
                        var device = CanvasDevice.GetSharedDevice();
                        CanvasBitmap? canvasBitmap = vectorService.CreateImageCanvasBitmap(device, desiredSize.Width, desiredSize.Height);

                        if (canvasBitmap is null)
                        {
                            return false;
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
                        Stream? stream = context.GetSourceStream();
                        context.UnrefSourceStream();

                        if (stream is null)
                        {
                            return false;
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
            }
            finally
            {
                record.Lock.ReleaseReaderLock();
                thumbnailStream?.Dispose();
                thumbnailStream = null;
            }
        }

        DecodingImageItem decodingItem = new()
        {
            CreateFunc = createFunc,
            CleanupAction = cleanupAction,
            Options = options,
            Uri = uri,
            StartTime = startTime,
        };

        sDecodeQueue.Enqueue(decodingItem);
        ScheduleDecoding();
        return true;
    }

    private static void CalculateDesiredDimension(double frameWidth, double frameHeight,
        StretchModeEnum stretchMode, double originWidth, double originHeight,
        out bool useOriginalSize, out Size desiredSize)
    {
        double imageRatio = originWidth / originHeight;
        double frameRatio = frameWidth / frameHeight;
        double desiredWidthRaw;
        double desiredHeightRaw;
        if (imageRatio > frameRatio == (stretchMode == StretchModeEnum.Uniform))
        {
            if (double.IsInfinity(frameWidth))
            {
                desiredWidthRaw = originWidth;
                desiredHeightRaw = originHeight;
                useOriginalSize = true;
            }
            else
            {
                desiredWidthRaw = frameWidth;
                desiredHeightRaw = desiredWidthRaw / imageRatio;
                useOriginalSize = desiredWidthRaw >= originWidth;
            }
        }
        else
        {
            if (double.IsInfinity(frameHeight))
            {
                desiredWidthRaw = originWidth;
                desiredHeightRaw = originHeight;
                useOriginalSize = true;
            }
            else
            {
                desiredHeightRaw = frameHeight;
                desiredWidthRaw = desiredHeightRaw * imageRatio;
                useOriginalSize = desiredHeightRaw >= originHeight;
            }
        }

        int desiredWidth = Math.Max(1, (int)Math.Round(desiredWidthRaw));
        int desiredHeight = Math.Max(1, (int)Math.Round(desiredHeightRaw));
        desiredSize = new(desiredWidth, desiredHeight);
    }

    private static void CalculateDefaultSizeForVector(float originWidth, float originHeight, out int width, out int height)
    {
        int defaultWidth = 764;
        int defaultHeight = 1080;

        if (!(float.IsFinite(originWidth) && float.IsFinite(originHeight) && originWidth > 0 && originHeight > 0))
        {
            width = defaultWidth;
            height = defaultHeight;
            return;
        }

        DisplayUtils.GetScreenSize(out int screenWidth, out int screenHeight);
        if (screenWidth <= 0 || screenHeight <= 0)
        {
            width = (int)originWidth;
            height = (int)originHeight;
            return;
        }

        float pageAspectRatio = originWidth / originHeight;
        float screenAspectRatio = (float)screenWidth / screenHeight;

        float targetWidth, targetHeight;
        if (pageAspectRatio > screenAspectRatio)
        {
            targetHeight = screenHeight;
            targetWidth = screenHeight / originHeight * originWidth;
        }
        else
        {
            targetWidth = screenWidth;
            targetHeight = screenWidth / originWidth * originHeight;
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

        if (!ValidateCacheRecord(record, fingerprint))
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

    private static void SaveThumbnail(CacheRequestContext context, string cacheEntryKey, string fingerprint,
        SizeF originalSize, LRUCache imageCache, ImageCacheDatabase.CacheRecord record)
    {
        if (!CalculateDesiredThumbnailSize(cacheEntryKey, originalSize, out Size thumbnailSize))
        {
            return;
        }

        SoftwareBitmap? thumbnailBitmap;
        IVectorImageService? vectorService = context.GetVectorService();
        if (vectorService is not null)
        {
            thumbnailBitmap = vectorService.CreateSoftwareBitmap(thumbnailSize.Width, thumbnailSize.Height);
        }
        else
        {
            BitmapDecoder? decoder = context.GetBitmapDecoder();
            if (decoder is null)
            {
                return;
            }

            thumbnailBitmap = CreateScaledSoftwareBitmap(decoder, thumbnailSize.Width, thumbnailSize.Height);
        }

        if (thumbnailBitmap is null)
        {
            return;
        }

        try
        {
            string entry = StringUtils.RandomFileName(16);
            using LRUCacheStream? cacheFileStream = imageCache.Put(entry);
            if (cacheFileStream is null)
            {
                return;
            }

            try
            {
                BitmapEncoder encoder = BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, cacheFileStream.AsRandomAccessStream()).AsTask().Result;
                encoder.SetSoftwareBitmap(thumbnailBitmap);
                encoder.IsThumbnailGenerated = false;
                encoder.FlushAsync().Wait();
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

    private static SoftwareBitmap? CreateScaledSoftwareBitmap(BitmapDecoder decoder, int scaledWidth, int scaledHeight)
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
            softwareBitmap = decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.DoNotColorManage).AsTask().Result;
        }
        catch (Exception ex)
        {
            Logger.E(TAG, ex);
        }

        return softwareBitmap;
    }

    private static bool CalculateDesiredThumbnailSize(string cacheEntryKey, SizeF originalSize, out Size thumbnailSize)
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
            return false;
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

    private static bool ValidateCacheRecord(ImageCacheDatabase.CacheRecord record, string? fingerprint)
    {
        string metaVersion = record.GetExt(ImageCacheExt.IMAGE_META_VERSION) ?? string.Empty;
        if (metaVersion != IMAGE_META_VERSION.ToString())
        {
            return false;
        }

        string metaFingerprint = record.GetExt(ImageCacheExt.IMAGE_META_FINGERPRINT) ?? string.Empty;
        if (fingerprint is not null && metaFingerprint != fingerprint)
        {
            return false;
        }

        return true;
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

    private static long GetCurrentTick()
    {
        return Environment.TickCount64;
    }

    private partial class CacheRequestContext(IImageSource source) : IDisposable
    {
        private readonly IImageSource _source = source;
        private string? _fingerprint = null;
        private Stream? _sourceStream = null;
        private BitmapDecoder? _bitmapDecoder = null;
        private IVectorImageService? _vectorService = null;

        public IImageSource Source => _source;

        public void Dispose()
        {
            _sourceStream?.Dispose();
            _sourceStream = null;
            _bitmapDecoder = null;
            _vectorService?.Dispose();
            _vectorService = null;
        }

        public string GetFingerprint()
        {
            if (_fingerprint is not null)
            {
                return _fingerprint;
            }

            _fingerprint = _source.CalculateFingerprint();
            return _fingerprint;
        }

        public Stream? GetSourceStream()
        {
            if (_sourceStream is not null)
            {
                _sourceStream.Seek(0, SeekOrigin.Begin);
                return _sourceStream;
            }

            try
            {
                _sourceStream = _source.OpenImageStream();
            }
            catch (Exception ex)
            {
                Logger.E(TAG, ex);
            }

            return _sourceStream;
        }

        public void UnrefSourceStream()
        {
            _sourceStream = null;
            _bitmapDecoder = null;
        }

        public BitmapDecoder? GetBitmapDecoder()
        {
            if (_bitmapDecoder is not null)
            {
                return _bitmapDecoder;
            }

            Stream? stream = GetSourceStream();
            if (stream is null)
            {
                return null;
            }

            try
            {
                _bitmapDecoder = BitmapDecoder.CreateAsync(stream.AsRandomAccessStream()).AsTask().Result;
            }
            catch (Exception ex)
            {
                Logger.E(TAG, ex);
            }

            return _bitmapDecoder;
        }

        public IVectorImageService? GetVectorService()
        {
            if (_vectorService is not null)
            {
                return _vectorService;
            }

            _vectorService = _source.OpenVectorService();
            return _vectorService;
        }
    }

    private class DecodingImageItem
    {
        public required Func<Task<DecodedImageModel>> CreateFunc;
        public required Action CleanupAction;
        public required LoadImageOptions Options;
        public string Uri = string.Empty;
        public long StartTime;
    }

    public class ImageMeta
    {
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required double DpiX { get; init; }
        public required double DpiY { get; init; }
        public required string Format { get; init; }
        public required int BitsPerPixel { get; init; }
        public required long Size { get; init; }
        public required int FrameCount { get; init; }
    }
}
