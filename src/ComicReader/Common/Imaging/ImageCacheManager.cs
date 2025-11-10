// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

using ComicReader.Common.Utils;
using ComicReader.SDK.Common.Caching;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Storage;
using ComicReader.SDK.Common.Threading;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

using Windows.Storage.Streams;

namespace ComicReader.Common.Imaging;

internal static class ImageCacheManager
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
    private static readonly ConcurrentQueue<RenderItem> sRenderQueue = new();

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
                IRandomAccessStream? stream = null;
                try
                {
                    stream = source.GetImageStream().Result;
                }
                catch (Exception e)
                {
                    Logger.E(TAG, "GetImageMeta", e);
                }

                if (stream is null)
                {
                    return null;
                }

                using (stream)
                {
                    stream.Seek(0);
                    Image? image = LoadImageFromStream(stream.AsStream());
                    if (image is null)
                    {
                        return null;
                    }

                    PutImageMetaToCacheRecord(record, sourceFingerprint, stream.Size, image);
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

    public static void LoadImage(CancellationSession.IToken token,
        IImageSource source, double frameWidth, double frameHeight, StretchModeEnum stretchMode,
        IImageResultHandler handler)
    {
        if (MainThreadUtils.IsMainThread())
        {
            Logger.F(TAG, "LoadImage cannot be called on main thread.");
            return;
        }

        if (token.IsCancellationRequested)
        {
            return;
        }

        long startTime = GetCurrentTick();
        string uri = source.GetUri();
        if (string.IsNullOrEmpty(uri))
        {
            Logger.E(TAG, "Image source URI is null or empty");
            return;
        }

        LRUCache? imageCache = GetImageLRUCache();
        if (imageCache is null)
        {
            Logger.F(TAG, "Image cache is null");
            return;
        }

        ImageCacheDatabase.CacheRecord? record = ImageCacheDatabase.GetOrCreate(source.GetUri());
        if (record is null)
        {
            Logger.F(TAG, "Cache record is null");
            return;
        }

        RenderItem renderItem;
        IRandomAccessStream? thumbnailStream = null;
        IRandomAccessStream? sourceStream = null;
        record.Lock.AcquireReaderLock(Timeout.Infinite);
        try
        {
            string sourceFingerprint = source.GetContentFingerprint();
            thumbnailStream = OpenThumbnailStreamFromCacheRecord(imageCache, record, sourceFingerprint, frameWidth, frameHeight, stretchMode, out bool requireThumbnail);
            if (thumbnailStream is null && requireThumbnail)
            {
                sourceStream = TryOpenImageStreamAsync(source).Result;
                if (sourceStream is null)
                {
                    Logger.E(TAG, "sourceStream is null");
                    return;
                }

                LockCookie lockCookie = record.Lock.UpgradeToWriterLock(Timeout.Infinite);
                try
                {
                    thumbnailStream = OpenThumbnailStreamFromCacheRecord(imageCache, record, sourceFingerprint, frameWidth, frameHeight, stretchMode, out bool _);
                    thumbnailStream ??= TryCreateThumbnail(imageCache, record, sourceStream, frameWidth, frameHeight, stretchMode, uri, sourceFingerprint);
                }
                finally
                {
                    record.Lock.DowngradeFromWriterLock(ref lockCookie);
                }
            }

            renderItem = new RenderItem
            {
                Token = token,
                Source = source,
                Uri = uri,
                FrameWidth = frameWidth,
                FrameHeight = frameHeight,
                StretchMode = stretchMode,
                ThumbnailStream = thumbnailStream,
                SourceStream = sourceStream,
                Handler = handler,
                StartTime = startTime,
            };
        }
        catch (Exception e)
        {
            Logger.F(TAG, "LoadImage", e);
            thumbnailStream?.Dispose();
            sourceStream?.Dispose();
            throw;
        }
        finally
        {
            record.Lock.ReleaseReaderLock();
        }

        sRenderQueue.Enqueue(renderItem);
        ScheduleRender();
    }

    private static void ScheduleRender()
    {
        if (Interlocked.CompareExchange(ref sPostMainThreadTask, 1, 0) == 1)
        {
            return;
        }

        _ = MainThreadUtils.PostInMainThreadAsync(async delegate
        {
            long startTime = GetCurrentTick();
            Interlocked.Exchange(ref sPostMainThreadTask, 0);
            // Only responsible for rendering tasks that have been queued before this point
            while (sRenderQueue.TryDequeue(out RenderItem? item))
            {
                await PerformRender(item);
                if (GetCurrentTick() - startTime > 50)
                {
                    // Keep main thread responsive.
                    if (sRenderQueue.TryPeek(out _))
                    {
                        ScheduleRender();
                    }
                    break;
                }
            }
        }, DispatcherQueuePriority.Low);
    }

    private static async Task PerformRender(RenderItem item)
    {
        BitmapImage? image = null;
        try
        {
            if (item.Token.IsCancellationRequested)
            {
                return;
            }

            if (item.ThumbnailStream != null)
            {
                image = await TryLoadImageFromStreamAsync(item.ThumbnailStream);
            }

            if (image == null)
            {
                item.SourceStream ??= await TryOpenImageStreamAsync(item.Source);

                if (item.SourceStream != null)
                {
                    image = await TryLoadImageFromStreamAsync(item.SourceStream);
                }
            }

            if (image == null)
            {
                Logger.E(TAG, "image is null");
                return;
            }

            CalculateDesiredDimension(item.FrameWidth, item.FrameHeight, item.StretchMode, image.PixelWidth, image.PixelHeight, out int desiredWidth, out int desiredHeight);
            if (desiredWidth != image.PixelWidth || desiredHeight != image.PixelHeight)
            {
                image.DecodePixelWidth = desiredWidth;
                image.DecodePixelHeight = desiredHeight;
            }
        }
        finally
        {
            item.ThumbnailStream?.Dispose();
            item.SourceStream?.Dispose();
        }

        if (item.Token.IsCancellationRequested)
        {
            Logger.I(TAG, $"task cancelled (time={GetCurrentTick() - item.StartTime},uri={item.Uri})");
            return;
        }

        item.Handler.OnSuccess(image);
    }

    private static IRandomAccessStream? OpenThumbnailStreamFromCacheRecord(LRUCache imageCache,
        ImageCacheDatabase.CacheRecord record, string sourceFingerprint, double frameWidth,
        double frameHeight, StretchModeEnum stretchMode, out bool requireThumbnail)
    {
        requireThumbnail = true;
        if (!string.IsNullOrEmpty(sourceFingerprint) && record.ImageCacheFingerprint != sourceFingerprint)
        {
            return null;
        }

        ImageMeta? meta = GetImageMetaFromCacheRecord(record, sourceFingerprint);
        if (meta is null)
        {
            return null;
        }

        requireThumbnail = false;
        IEnumerable<string> cacheEntryKeys = CalculateCacheEntryKeys(frameWidth, frameHeight, stretchMode, meta.Width, meta.Height);
        foreach (string cacheEntryKey in cacheEntryKeys)
        {
            requireThumbnail = true;
            string? entry = record.GetCacheEntry(cacheEntryKey);
            if (!string.IsNullOrEmpty(entry))
            {
                IRandomAccessStream? thumbnailStream = imageCache.Get(entry);
                if (thumbnailStream != null)
                {
                    return thumbnailStream;
                }
            }
        }

        return null;
    }

    private static IRandomAccessStream? TryCreateThumbnail(LRUCache imageCache,
        ImageCacheDatabase.CacheRecord record, IRandomAccessStream sourceStream,
        double frameWidth, double frameHeight, StretchModeEnum stretchMode,
        string cacheKey, string sourceFingerprint)
    {
        sourceStream.Seek(0);
        Image? image = LoadImageFromStream(sourceStream.AsStream());
        if (image is null)
        {
            return null;
        }

        try
        {
            PutImageMetaToCacheRecord(record, sourceFingerprint, sourceStream.Size, image);

            int sourceWidth = image.Width;
            int sourceHeight = image.Height;
            IEnumerable<string> cacheEntryKeys = CalculateCacheEntryKeys(frameWidth, frameHeight, stretchMode, sourceWidth, sourceHeight);
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
                        byte[] outByteArray = new byte[cacheStream.Length];
                        cacheStream.Read(outByteArray, 0, outByteArray.Length);
                        string tempEntry = StringUtils.RandomFileName(16);
                        using ILRUInputStream cacheFileStream = imageCache.Put(tempEntry);
                        if (cacheFileStream == null)
                        {
                            Logger.F(TAG, "TryCreateImageCache cacheFileStream is null");
                        }
                        else
                        {
                            cacheFileStream.WriteAsync(outByteArray.AsBuffer()).Wait();
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
            catch (Exception)
            {
                cacheStream?.Dispose();
                throw;
            }

            return cacheStream?.AsRandomAccessStream();
        }
        finally
        {
            image.Dispose();
        }
    }

    private static Image? LoadImageFromStream(Stream stream)
    {
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

    private static IEnumerable<string> CalculateCacheEntryKeys(double frameWidth, double frameHeight, StretchModeEnum stretchMode, int originWidth, int originHeight)
    {
        CalculateDesiredDimension(frameWidth, frameHeight, stretchMode, originWidth, originHeight, out int desiredWidth, out int desiredHeight);
        return ImageCacheStrategy.CalculateCacheEntryKeys(desiredWidth, desiredHeight, originWidth, originHeight);
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

    private static async Task<IRandomAccessStream?> TryOpenImageStreamAsync(IImageSource source)
    {
        try
        {
            return await source.GetImageStream();
        }
        catch (Exception e)
        {
            Logger.E(TAG, "TryLoadImageFromFile", e);
        }

        return null;
    }

    private static async Task<BitmapImage?> TryLoadImageFromStreamAsync(IRandomAccessStream stream)
    {
        BitmapImage? image = new();
        try
        {
            stream.Seek(0);
            await image.SetSourceAsync(stream);
        }
        catch (Exception e)
        {
            Logger.F(TAG, "TryLoadImageFromStream", e);
            image = null;
        }

        return image;
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

        int ReadExtInterger(string key, int defaultValue)
        {
            string? value = record.GetExt(key);
            if (!string.IsNullOrEmpty(value) && int.TryParse(value, out int result))
            {
                return result;
            }

            return defaultValue;
        }

        long ReadExtLong(string key, long defaultValue)
        {
            string? value = record.GetExt(key);
            if (!string.IsNullOrEmpty(value) && long.TryParse(value, out long result))
            {
                return result;
            }

            return defaultValue;
        }

        int width = ReadExtInterger(ImageCacheExt.IMAGE_META_WIDTH, 0);
        int height = ReadExtInterger(ImageCacheExt.IMAGE_META_HEIGHT, 0);
        int dpiX = ReadExtInterger(ImageCacheExt.IMAGE_META_DPI_X, 0);
        int dpiY = ReadExtInterger(ImageCacheExt.IMAGE_META_DPI_Y, 0);
        int bitsPerPixel = ReadExtInterger(ImageCacheExt.IMAGE_META_BITS_PER_PIXEL, 0);
        long size = ReadExtLong(ImageCacheExt.IMAGE_META_SIZE, 0);
        string decoderName = record.GetExt(ImageCacheExt.IMAGE_META_DECODER_NAME) ?? string.Empty;

        return new(width, height, dpiX, dpiY, decoderName, bitsPerPixel, size);
    }

    private static void PutImageMetaToCacheRecord(ImageCacheDatabase.CacheRecord record, string sourceFingerprint, ulong size, Image image)
    {
        SixLabors.ImageSharp.Metadata.ImageMetadata? metadata = image.Metadata;
        if (metadata == null)
        {
            Logger.F(TAG, "Image metadata is null");
            return;
        }

        SixLabors.ImageSharp.Formats.PixelTypeInfo pixelType = image.PixelType;
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

        metadata.ResolutionUnits = SixLabors.ImageSharp.Metadata.PixelResolutionUnit.PixelsPerInch;
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
        StretchModeEnum stretchMode, int originWidth, int originHeight, out int desiredWidth, out int desiredHeight)
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

        desiredWidth = (int)desiredWidthRaw;
        desiredHeight = (int)desiredHeightRaw;
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

    private class RenderItem
    {
        public required CancellationSession.IToken Token;
        public required IImageSource Source;
        public required string Uri;
        public double FrameWidth;
        public double FrameHeight;
        public StretchModeEnum StretchMode;
        public IRandomAccessStream? ThumbnailStream;
        public IRandomAccessStream? SourceStream;
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
