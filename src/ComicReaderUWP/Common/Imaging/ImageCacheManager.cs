// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.SDK.Common.Caching;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Storage;
using ComicReaderUWP.SDK.Common.Threading;
using ComicReaderUWP.SDK.Common.Utils;

using Microsoft.UI.Dispatching;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

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
            // If image meta is already cached, return it directly
            string sourceFingerprint = source.GetContentFingerprint();
            ImageMeta? meta = GetImageMetaFromCacheRecord(record, sourceFingerprint);
            if (meta is not null)
            {
                return meta;
            }

            LockCookie lockCookie = record.Lock.UpgradeToWriterLock(Timeout.Infinite);
            try
            {
                // Double check
                meta = GetImageMetaFromCacheRecord(record, sourceFingerprint);
                if (meta is not null)
                {
                    return meta;
                }

                // Open image stream to get image meta
                Stream? stream = null;
                try
                {
                    stream = source.GetImageStream();
                }
                catch (Exception e)
                {
                    Logger.F(TAG, "GetImageMeta#GetImageStream", e);
                }

                if (stream is null)
                {
                    return null;
                }

                using (stream)
                {
                    using Image? image = LoadImageFromStream(stream);
                    if (image is null)
                    {
                        return null;
                    }

                    PutImageMetaToCacheRecord(record, sourceFingerprint, stream.Length, image);
                }

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

    public static void LoadImage(CancellationSession.IToken token, IImageSource source,
        double frameWidth, double frameHeight, StretchModeEnum stretchMode, IImageResultHandler handler)
    {
        bool enqueued = LoadImageInternal(token, source, frameWidth, frameHeight, stretchMode, handler);
        if (!enqueued)
        {
            handler.OnFailure();
        }
    }

    private static bool LoadImageInternal(CancellationSession.IToken token, IImageSource source,
        double frameWidth, double frameHeight, StretchModeEnum stretchMode, IImageResultHandler handler)
    {
        if (MainThreadUtils.IsMainThread())
        {
            Logger.F(TAG, "LoadImage cannot be called on main thread.");
            return false;
        }

        if (token.IsCancellationRequested)
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

        DecodingImageItem decodingItem;
        Stream? thumbnailStream = null;
        Stream? sourceStream = null;
        record.Lock.AcquireReaderLock(Timeout.Infinite);
        try
        {
            string sourceFingerprint = source.GetContentFingerprint();

            ImageMeta? meta = null;
            if (string.IsNullOrEmpty(sourceFingerprint) || record.ImageCacheFingerprint != sourceFingerprint)
            {
                meta = GetImageMetaFromCacheRecord(record, sourceFingerprint);
            }

            bool requireThumbnail = true;
            Size? desiredSize = null;
            if (meta is not null)
            {
                CalculateDesiredDimension(frameWidth, frameHeight, stretchMode, meta.Width, meta.Height, out Size desiredSizeOut);
                desiredSize = desiredSizeOut;
                thumbnailStream = OpenThumbnailStreamFromCacheRecord(imageCache, record, meta, desiredSizeOut, out requireThumbnail);
            }

            if (thumbnailStream is null)
            {
                sourceStream = TryOpenImageStreamAsync(source);
            }

            Stream? imageStream = thumbnailStream ?? sourceStream;
            if (imageStream is null)
            {
                Logger.E(TAG, "imageStream is null");
                return false;
            }

            bool needResize = desiredSize is null;
            DecoderOptions opts = new()
            {
                TargetSize = desiredSize,
            };
            Image<Bgra32>? image = LoadBgra32ImageFromStream(imageStream, opts);
            if (image is null)
            {
                return false;
            }

            if (desiredSize is null)
            {
                CalculateDesiredDimension(frameWidth, frameHeight, stretchMode, image.Width, image.Height, out Size desiredSizeOut);
                desiredSize = desiredSizeOut;
            }

            if (requireThumbnail && thumbnailStream is null)
            {
                LockCookie lockCookie = record.Lock.UpgradeToWriterLock(Timeout.Infinite);
                try
                {
                    if (meta is null)
                    {
                        PutImageMetaToCacheRecord(record, sourceFingerprint, imageStream.Length, image);
                    }

                    TryCreateThumbnail(imageCache, record, image, desiredSize.Value, sourceFingerprint);
                }
                finally
                {
                    record.Lock.DowngradeFromWriterLock(ref lockCookie);
                }
            }

            if (needResize)
            {
                image.Mutate(x => x.Resize(desiredSize.Value));
            }

            decodingItem = new DecodingImageItem
            {
                Token = token,
                Uri = uri,
                FrameWidth = frameWidth,
                FrameHeight = frameHeight,
                StretchMode = stretchMode,
                Image = image,
                Handler = handler,
                StartTime = startTime,
            };
        }
        catch (Exception e)
        {
            Logger.F(TAG, "LoadImage", e);
            throw;
        }
        finally
        {
            record.Lock.ReleaseReaderLock();
            thumbnailStream?.Dispose();
            sourceStream?.Dispose();
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
                await PerformDecoding(item);

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

    private static async Task PerformDecoding(DecodingImageItem item)
    {
        void CleanUpAndDispatchFailureEvent()
        {
            item.Image?.Dispose();
            item.Handler.OnFailure();
        }

        if (item.Token.IsCancellationRequested)
        {
            Logger.I(TAG, $"task cancelled (time={GetCurrentTick() - item.StartTime},uri={item.Uri})");
            CleanUpAndDispatchFailureEvent();
            return;
        }

        DecodedImageModel model = new()
        {
            Image = item.Image,
        };

        item.Handler.OnSuccess(model);
    }

    private static Stream? OpenThumbnailStreamFromCacheRecord(LRUCache imageCache, ImageCacheDatabase.CacheRecord record,
        ImageMeta meta, Size desiredSize, out bool requireThumbnail)
    {
        requireThumbnail = false;
        IEnumerable<string> cacheEntryKeys = ImageCacheStrategy.CalculateCacheEntryKeys(desiredSize.Width, desiredSize.Height, meta.Width, meta.Height);
        foreach (string cacheEntryKey in cacheEntryKeys)
        {
            requireThumbnail = true;
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

    private static void TryCreateThumbnail(LRUCache imageCache, ImageCacheDatabase.CacheRecord record,
        Image image, Size desiredSize, string sourceFingerprint)
    {
        int sourceWidth = image.Width;
        int sourceHeight = image.Height;
        IEnumerable<string> cacheEntryKeys = ImageCacheStrategy.CalculateCacheEntryKeys(desiredSize.Width, desiredSize.Height, sourceWidth, sourceHeight);
        string? cacheEntryKey = cacheEntryKeys.FirstOrDefault();

        MemoryStream? cacheStream = null;
        if (!string.IsNullOrEmpty(cacheEntryKey))
        {
            cacheStream = CreateImageCacheStream(cacheEntryKey, sourceWidth, sourceHeight, image);
        }

        try
        {
            // Save thumbnail to file if possible
            string? entry = null;
            if (cacheStream is not null)
            {
                try
                {
                    cacheStream.Seek(0, SeekOrigin.Begin);
                    string tempEntry = StringUtils.RandomFileName(16);
                    using LRUCacheStream? cacheFileStream = imageCache.Put(tempEntry);
                    if (cacheFileStream == null)
                    {
                        Logger.F(TAG, "TryCreateImageCache cacheFileStream is null");
                    }
                    else
                    {
                        cacheStream.CopyTo(cacheFileStream);
                    }

                    entry = tempEntry;
                }
                catch (Exception e)
                {
                    Logger.F(TAG, "TryCreateImageCache", e);
                }
            }

            if (!string.IsNullOrEmpty(cacheEntryKey) && !string.IsNullOrEmpty(entry))
            {
                record.ImageCacheFingerprint = sourceFingerprint;
                record.PutCacheEntry(cacheEntryKey, entry);
            }

            record.Save();
        }
        finally
        {
            cacheStream?.Dispose();
        }
    }

    private static Image? LoadImageFromStream(Stream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
        Image? image = null;
        try
        {
            image = Image.Load(stream);
        }
        catch (Exception ex)
        {
            Logger.F(TAG, "LoadImageFromStream", ex);
        }

        if (image is null)
        {
            return null;
        }

        image.Mutate(p => p.AutoOrient()); // Rotation EXIF for JPEG
        return image;
    }

    private static Image<Bgra32>? LoadBgra32ImageFromStream(Stream stream, DecoderOptions options)
    {
        stream.Seek(0, SeekOrigin.Begin);
        Image<Bgra32>? image = null;
        try
        {
            image = Image.Load<Bgra32>(options, stream);
        }
        catch (Exception ex)
        {
            Logger.F(TAG, "LoadImageFromStream", ex);
        }

        if (image is null)
        {
            return null;
        }

        image.Mutate(p => p.AutoOrient()); // Rotation EXIF for JPEG
        return image;
    }

    private static MemoryStream? CreateImageCacheStream(string cacheEntryKey, int sourceWidth, int sourceHeight, Image image)
    {
        int cacheResolution = ImageCacheStrategy.GetCacheResolution(cacheEntryKey);
        if (cacheResolution <= 0)
        {
            return null;
        }

        int sourceResolution = sourceWidth * sourceHeight;
        if (sourceResolution <= cacheResolution)
        {
            return null;
        }

        double scaleRatio = (double)cacheResolution / sourceResolution;
        double dimensionRatio = Math.Sqrt(scaleRatio);
        int aspectHeight = (int)Math.Floor(sourceHeight * dimensionRatio);
        int aspectWidth = (int)Math.Floor(sourceWidth * dimensionRatio);

        MemoryStream? memoryStream = new();
        try
        {
            image.Mutate(x => x.Resize(aspectWidth, aspectHeight));
            image.Save(memoryStream, image.Metadata.DecodedImageFormat!);
        }
        catch (Exception e)
        {
            Logger.F(TAG, "CreateImageCacheStream", e);
            memoryStream.Dispose();
            memoryStream = null;
        }

        return memoryStream;
    }

    private static Stream? TryOpenImageStreamAsync(IImageSource source)
    {
        try
        {
            return source.GetImageStream();
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

        if (!TryReadIntExt(ImageCacheExt.IMAGE_META_WIDTH, out int width))
        {
            return null;
        }

        if (!TryReadIntExt(ImageCacheExt.IMAGE_META_HEIGHT, out int height))
        {
            return null;
        }

        if (!TryReadIntExt(ImageCacheExt.IMAGE_META_DPI_X, out int dpiX))
        {
            return null;
        }

        if (!TryReadIntExt(ImageCacheExt.IMAGE_META_DPI_Y, out int dpiY))
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

    private static void PutImageMetaToCacheRecord(ImageCacheDatabase.CacheRecord record, string sourceFingerprint, long size, Image image)
    {
        ImageMetadata? metadata = image.Metadata;
        if (metadata == null)
        {
            Logger.F(TAG, "Image metadata is null");
            return;
        }

        PixelTypeInfo pixelType = image.PixelType;
        if (pixelType == null)
        {
            Logger.F(TAG, "Image pixel type is null");
            return;
        }

        int width = image.Width;
        int height = image.Height;
        if (width <= 0 || height <= 0)
        {
            Logger.F(TAG, $"Invalid image dimensions: width={width}, height={height}");
            return;
        }

        metadata.ResolutionUnits = PixelResolutionUnit.PixelsPerInch;
        record.PutExt(ImageCacheExt.IMAGE_META_VERSION, IMAGE_META_VERSION.ToString());
        record.PutExt(ImageCacheExt.IMAGE_META_FINGERPRINT, sourceFingerprint);
        record.PutExt(ImageCacheExt.IMAGE_META_WIDTH, width.ToString());
        record.PutExt(ImageCacheExt.IMAGE_META_HEIGHT, height.ToString());
        record.PutExt(ImageCacheExt.IMAGE_META_DPI_X, metadata.HorizontalResolution.ToString());
        record.PutExt(ImageCacheExt.IMAGE_META_DPI_Y, metadata.VerticalResolution.ToString());
        record.PutExt(ImageCacheExt.IMAGE_META_BITS_PER_PIXEL, pixelType.BitsPerPixel.ToString());
        record.PutExt(ImageCacheExt.IMAGE_META_DECODER_NAME, metadata.DecodedImageFormat?.Name ?? string.Empty);
        record.PutExt(ImageCacheExt.IMAGE_META_SIZE, size.ToString());
    }

    private static void CalculateDesiredDimension(double frameWidth, double frameHeight,
        StretchModeEnum stretchMode, int originWidth, int originHeight, out Size desiredSize)
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
            }
            else
            {
                desiredWidthRaw = frameWidth * rawPixelsPerViewPixel;
                desiredHeightRaw = desiredWidthRaw / imageRatio;
            }
        }
        else
        {
            if (double.IsInfinity(frameHeight))
            {
                desiredWidthRaw = originWidth;
                desiredHeightRaw = originHeight;
            }
            else
            {
                desiredHeightRaw = frameHeight * rawPixelsPerViewPixel;
                desiredWidthRaw = desiredHeightRaw * imageRatio;
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
        public required CancellationSession.IToken Token;
        public required string Uri;
        public required Image<Bgra32> Image;
        public double FrameWidth;
        public double FrameHeight;
        public StretchModeEnum StretchMode;
        public required IImageResultHandler Handler;
        public long StartTime;
    }

    public class ImageMeta(int width, int height, int dpiX, int dpiY, string format, int bitsPerPixel, long size)
    {
        public int Width => width;
        public int Height => height;
        public int DpiX => dpiX;
        public int DpiY => dpiY;
        public string Format => format;
        public int BitsPerPixel => bitsPerPixel;
        public long Size => size;
    }
}
