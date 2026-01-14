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
using ComicReaderUWP.SDK.Common.Caching;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Storage;
using ComicReaderUWP.SDK.Common.Threading;
using ComicReaderUWP.SDK.Common.Utils;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.Graphics.Imaging;

namespace ComicReaderUWP.Common.Imaging;

internal static partial class ImageCacheManager
{
    private const string TAG = "ImageCacheManager";
    private const int VERSION = 1;
    private const int IMAGE_META_VERSION = 1;
    private const string IMAGES_FOLDER_NAME = "images";
    private const string MAIN_DATABASE_FILE_NAME = "db_main.db";
    private const long MAX_CACHE_SIZE = 1024 * 1024 * 1024;

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
        int version = clear ? 0 : ReadVersion(versionFilePath);
        if (version != VERSION)
        {
            switch (version)
            {
                case 0:
                    DeleteDirectory(Path.Combine(StorageLocation.LocalCacheFolderPath, "images"), throwOnError: false);
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

    public static ImageMeta? GetImageMeta(IImageSource source)
    {
        if (MainThreadUtils.IsMainThread())
        {
            Logger.F(TAG, "GetImageMeta cannot be called on main thread.");
            return null;
        }

        ImageCacheDatabase.CacheRecord? record = ImageCacheDatabase.GetOrCreate(source.GetUri());
        if (record is null)
        {
            Logger.F(TAG, "Cache record is null");
            return null;
        }

        record.Lock.AcquireReaderLock(Timeout.Infinite);
        try
        {
            string sourceFingerprint = source.GetContentFingerprint();
            ImageMeta? meta = GetImageMetaFromCacheRecord(record, sourceFingerprint);
            if (meta is not null)
            {
                return meta;
            }

            LockCookie lockCookie = record.Lock.UpgradeToWriterLock(Timeout.Infinite);
            try
            {
                meta = GetImageMetaFromCacheRecord(record, sourceFingerprint);
                if (meta is not null)
                {
                    return meta;
                }

                using Stream? stream = OpenImageStream(source);
                if (stream is null)
                {
                    return null;
                }

                BitmapDecoder? decoder = CreateBitmapDecoder(stream);
                if (decoder is null)
                {
                    return null;
                }

                PutImageMetaToCacheRecord(record, sourceFingerprint, stream.Length, decoder);
                ImageMeta? imageMeta = GetImageMetaFromCacheRecord(record, sourceFingerprint);
                if (imageMeta is null)
                {
                    Logger.F(TAG, "Failed to get image meta from cache record after saving");
                    return null;
                }

                record.Save();
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

    public static void LoadImage(IImageSource source, LoadImageOptions options)
    {
        options = options.Clone();
        bool enqueued = LoadImageInternal(source, options);
        if (!enqueued)
        {
            options.Handler.OnFailure();
        }
    }

    private static bool LoadImageInternal(IImageSource source, LoadImageOptions options)
    {
        if (MainThreadUtils.IsMainThread())
        {
            Logger.F(TAG, "LoadImage cannot be called on main thread.");
            return false;
        }

        if (options.Token.IsCancellationRequested)
        {
            return false;
        }

        long startTime = GetCurrentTick();
        string uri = source.GetUri();
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

        ImageCacheDatabase.CacheRecord? record = ImageCacheDatabase.GetOrCreate(source.GetUri());
        if (record is null)
        {
            Logger.F(TAG, "Cache record is null");
            return false;
        }

        string sourceFingerprint = source.GetContentFingerprint();

        Stream? thumbnailStream = null;
        Stream? sourceStream = null;
        DecodingImageItem decodingItem;
        record.Lock.AcquireReaderLock(Timeout.Infinite);
        try
        {
            BitmapDecoder? decoder = null;

            // Ensure image metadata
            ImageMeta? meta = GetImageMetaFromCacheRecord(record, sourceFingerprint);
            if (meta is null)
            {
                sourceStream ??= OpenImageStream(source);
                if (sourceStream is null)
                {
                    return false;
                }

                decoder ??= CreateBitmapDecoder(sourceStream);
                if (decoder is null)
                {
                    return false;
                }

                LockCookie lockCookie = record.Lock.UpgradeToWriterLock(Timeout.Infinite);
                try
                {
                    PutImageMetaToCacheRecord(record, sourceFingerprint, sourceStream.Length, decoder);
                    meta = GetImageMetaFromCacheRecord(record, sourceFingerprint);
                    if (meta is null)
                    {
                        Logger.F(TAG, "Failed to get image meta from cache record after saving");
                        return false;
                    }

                    record.Save();
                }
                finally
                {
                    record.Lock.DowngradeFromWriterLock(ref lockCookie);
                }
            }

            // Calculate targe size
            CalculateDesiredDimension(
                options.FrameWidth,
                options.FrameHeight,
                options.StretchMode,
                meta.Width,
                meta.Height,
                out bool useOriginalSize,
                out Size desiredSize);

            // Schedule ImageSource creation
            Func<Task<DecodedImageModel>> createFunc;
            Action cleanupAction;
            if (useOriginalSize)
            {
                sourceStream ??= OpenImageStream(source);
                if (sourceStream is null)
                {
                    return false;
                }

                Stream imageStream = sourceStream;
                sourceStream = null;
                createFunc = async () =>
                {
                    BitmapImage bitmap = new();
                    await bitmap.SetSourceAsync(imageStream.AsRandomAccessStream());
                    return new() { Source = bitmap };
                };
                cleanupAction = imageStream.Dispose;
            }
            else
            {
                thumbnailStream = OpenThumbnailStreamFromCacheRecord(imageCache, record, meta, desiredSize, out List<string> cacheEntryKeys);
                if (thumbnailStream is not null)
                {
                    Stream imageStream = thumbnailStream;
                    thumbnailStream = null;
                    createFunc = async () =>
                    {
                        BitmapImage bitmap = new();
                        await bitmap.SetSourceAsync(imageStream.AsRandomAccessStream());
                        return new() { Source = bitmap };
                    };
                    cleanupAction = imageStream.Dispose;
                }
                else
                {
                    sourceStream ??= OpenImageStream(source);
                    if (sourceStream is null)
                    {
                        return false;
                    }

                    decoder ??= CreateBitmapDecoder(sourceStream);
                    if (decoder is null)
                    {
                        return false;
                    }

                    SoftwareBitmap? softwareBitmap = CreateScaledSoftwareBitmap(decoder, desiredSize.Width, desiredSize.Height);
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

                    if (cacheEntryKeys.Count > 0)
                    {
                        string cacheEntryKey = cacheEntryKeys[0];
                        Size originalSize = new(meta.Width, meta.Height);
                        LockCookie lockCookie = record.Lock.UpgradeToWriterLock(Timeout.Infinite);
                        try
                        {
                            SaveThumbnail(decoder, cacheEntryKey, sourceFingerprint, originalSize, imageCache, record);
                        }
                        finally
                        {
                            record.Lock.DowngradeFromWriterLock(ref lockCookie);
                        }
                    }
                }
            }

            decodingItem = new DecodingImageItem
            {
                CreateFunc = createFunc,
                CleanupAction = cleanupAction,
                Options = options,
                Uri = uri,
                StartTime = startTime,
            };
        }
        finally
        {
            record.Lock.ReleaseReaderLock();
            thumbnailStream?.Dispose();
            thumbnailStream = null;
            sourceStream?.Dispose();
            sourceStream = null;
        }

        sDecodeQueue.Enqueue(decodingItem);
        ScheduleDecoding();
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

    private static Stream? OpenThumbnailStreamFromCacheRecord(LRUCache imageCache, ImageCacheDatabase.CacheRecord record,
        ImageMeta meta, Size desiredSize, out List<string> cacheEntryKeys)
    {
        cacheEntryKeys = ImageCacheStrategy.CalculateCacheEntryKeys(desiredSize.Width, desiredSize.Height, meta.Width, meta.Height);
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

    private static void SaveThumbnail(BitmapDecoder decoder, string cacheEntryKey, string fingerprint,
        Size originalSize, LRUCache imageCache, ImageCacheDatabase.CacheRecord record)
    {
        if (!CalculateDesiredThumbnailSize(cacheEntryKey, originalSize.Width, originalSize.Height, out Size desiredSize))
        {
            return;
        }

        using SoftwareBitmap? thumbnailBitmap = CreateScaledSoftwareBitmap(decoder, desiredSize.Width, desiredSize.Height);
        if (thumbnailBitmap is null)
        {
            return;
        }

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
        catch (Exception e)
        {
            Logger.F(TAG, "TryCreateImageCache", e);
            return;
        }

        record.ImageCacheFingerprint = fingerprint;
        record.PutCacheEntry(cacheEntryKey, entry);
        record.Save();
    }

    private static BitmapDecoder? CreateBitmapDecoder(Stream stream)
    {
        BitmapDecoder? decoder = null;
        try
        {
            decoder = BitmapDecoder.CreateAsync(stream.AsRandomAccessStream()).AsTask().Result;
        }
        catch (Exception e)
        {
            Logger.E(TAG, e);
        }

        return decoder;
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

    private static bool CalculateDesiredThumbnailSize(string cacheEntryKey, int originalWidth, int originalHeight, out Size desiredSize)
    {
        desiredSize = new();

        int cacheResolution = ImageCacheStrategy.GetCacheResolution(cacheEntryKey);
        if (cacheResolution <= 0)
        {
            return false;
        }

        int sourceResolution = originalWidth * originalHeight;
        if (sourceResolution <= cacheResolution)
        {
            return false;
        }

        double scaleRatio = (double)cacheResolution / sourceResolution;
        double dimensionRatio = Math.Sqrt(scaleRatio);
        int aspectHeight = (int)Math.Floor(originalHeight * dimensionRatio);
        int aspectWidth = (int)Math.Floor(originalWidth * dimensionRatio);
        desiredSize = new(aspectWidth, aspectHeight);
        return true;
    }

    private static Stream? OpenImageStream(IImageSource source)
    {
        try
        {
            return source.OpenImageStream();
        }
        catch (Exception e)
        {
            Logger.E(TAG, "TryLoadImageFromFile", e);
        }

        return null;
    }

    private static ImageMeta? GetImageMetaFromCacheRecord(ImageCacheDatabase.CacheRecord record, string sourceFingerprint)
    {
        string metaVersion = record.GetExt(ImageCacheExt.IMAGE_META_VERSION) ?? string.Empty;
        if (metaVersion != IMAGE_META_VERSION.ToString())
        {
            return null;
        }

        string metaFingerprint = record.GetExt(ImageCacheExt.IMAGE_META_FINGERPRINT) ?? string.Empty;
        if (!string.IsNullOrEmpty(sourceFingerprint) && metaFingerprint != sourceFingerprint)
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

        return new(width, height, dpiX, dpiY, decoderName, bitsPerPixel, size);
    }

    private static void PutImageMetaToCacheRecord(ImageCacheDatabase.CacheRecord record, string sourceFingerprint, long size, BitmapDecoder decoder)
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
    }

    private static void CalculateDesiredDimension(double frameWidth, double frameHeight,
        StretchModeEnum stretchMode, int originWidth, int originHeight,
        out bool useOriginalSize, out Size desiredSize)
    {
        double rawPixelsPerViewPixel = DisplayUtils.GetRawPixelPerPixel();
        double imageRatio = (double)originWidth / originHeight;
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
                desiredWidthRaw = frameWidth * rawPixelsPerViewPixel;
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
                desiredHeightRaw = frameHeight * rawPixelsPerViewPixel;
                desiredWidthRaw = desiredHeightRaw * imageRatio;
                useOriginalSize = desiredHeightRaw >= originHeight;
            }
        }

        desiredSize = new((int)desiredWidthRaw, (int)desiredHeightRaw);
    }

    private static LRUCache? GetImageLRUCache()
    {
        {
            LRUCache? cache = sImageCache;
            if (cache != null)
            {
                return cache;
            }
        }

        lock (sLock)
        {
            LRUCache? cache = sImageCache;
            if (cache != null)
            {
                return cache;
            }

            string folderPath = Path.Combine(_cacheDirectoryPath, IMAGES_FOLDER_NAME);
            try
            {
                Directory.CreateDirectory(folderPath);
            }
            catch (Exception e)
            {
                Logger.AssertNotReachHere("266A35BFD509B7A0", e);
                return null;
            }

            cache = new(folderPath, MAX_CACHE_SIZE);
            sImageCache = cache;
            TaskDispatcher.LongRunningThreadPool.Submit("CleanImageCache", delegate
            {
                cache.Cleanup();
            });

            return cache;
        }
    }

    private static int ReadVersion(string versionFilePath)
    {
        if (!File.Exists(versionFilePath))
        {
            return 0;
        }

        string versionContent;
        try
        {
            versionContent = File.ReadAllText(versionFilePath);
        }
        catch (Exception e)
        {
            Logger.E(TAG, e);
            return 0;
        }

        if (int.TryParse(versionContent, out int version))
        {
            return version;
        }

        return 0;
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
        catch (Exception e)
        {
            if (throwOnError)
            {
                throw;
            }

            Logger.E(TAG, $"Failed to delete directory: {path}", e);
        }
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
        public string Uri = string.Empty;
        public long StartTime;
    }

    public class ImageMeta(int width, int height, double dpiX, double dpiY, string format, int bitsPerPixel, long size)
    {
        public int Width => width;
        public int Height => height;
        public double DpiX => dpiX;
        public double DpiY => dpiY;
        public string Format => format;
        public int BitsPerPixel => bitsPerPixel;
        public long Size => size;
    }
}
