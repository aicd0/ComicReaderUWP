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

internal static partial class ImageLoader
{
    private const string TAG = nameof(ImageLoader);
    private const int VERSION = 1;
    private const int IMAGE_META_VERSION = 2;
    private const string IMAGES_FOLDER_NAME = "Images";
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

    public static Task LoadImage(IImageSource source, LoadImageOptions options)
    {
        options = options.Clone();

        ImageLoaderSchedulerGroup group = options.SchedulerGroup ?? source.PreferredSchedulerGroup;

        return ImageLoaderScheduler.Submit(async () =>
        {
            options.FrameWidth *= 1.2;
            options.FrameHeight *= 1.2;

            using CacheRequestContext context = new(source);
            if (!await LoadImage(context, options))
            {
                CoroutineUtils.RunInMainThread(options.Handler.OnFailure);
            }
        }, group, options.Priority);
    }

    public static Task<ImageMeta?> LoadImageMeta(IImageSource source, LoadImageMetaOptions options)
    {
        options = options.Clone();

        ImageLoaderSchedulerGroup group = options.SchedulerGroup ?? source.PreferredSchedulerGroup;

        return ImageLoaderScheduler.Submit(async () =>
        {
            using CacheRequestContext context = new(source);
            return await GetImageMeta(context);
        }, group, options.Priority);
    }

    private static async Task<ImageMeta?> GetImageMeta(CacheRequestContext context)
    {
        ImageCacheDatabase.CacheRecord? record = ImageCacheDatabase.GetOrCreate(context.Source.Uri);
        if (record is null)
        {
            return null;
        }

        return await record.Enqueue(async () =>
        {
            string? fingerprint = context.Source.ValidateFingerprint ? await context.GetFingerprint() : null;
            ImageMeta? meta = CreateImageMetaFromCacheRecord(record, fingerprint);
            if (meta is not null)
            {
                return meta;
            }

            Stream? stream = await context.GetSourceStream();
            if (stream is null)
            {
                return null;
            }

            BitmapDecoder? decoder = await context.GetBitmapDecoder();
            if (decoder is null)
            {
                return null;
            }

            fingerprint ??= await context.GetFingerprint();
            record.Clear();
            SaveImageMetaToCacheRecord(record, fingerprint, stream.Length, decoder);
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
            "Microsoft HEIF Decoder" => "HEIF",
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

    private static async Task<bool> LoadImage(CacheRequestContext context, LoadImageOptions options)
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

        ImageMeta? meta = await GetImageMeta(context);
        if (meta is null)
        {
            return false;
        }

        Size originalSize = new(meta.Width, meta.Height);
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
                Stream? stream = await context.GetSourceStream();
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

            string? fingerprint = context.Source.ValidateFingerprint ? await context.GetFingerprint() : null;

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
                            await SaveThumbnail(context, cacheEntryKey, fingerprint, originalSize, imageCache, record);
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
                            Stream? stream = await context.GetSourceStream();
                            context.UnrefSourceStream();

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

    private static async Task SaveThumbnail(CacheRequestContext context, string cacheEntryKey, string fingerprint,
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
            BitmapDecoder? decoder = await context.GetBitmapDecoder();
            if (decoder is null)
            {
                return;
            }

            thumbnailBitmap = await CreateScaledSoftwareBitmap(decoder, thumbnailSize.Width, thumbnailSize.Height);
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

        private bool _connectionInitialized = false;
        private IImageConnection? _connection = null;

        private string? _fingerprint = null;
        private Stream? _sourceStream = null;
        private BitmapDecoder? _bitmapDecoder = null;
        private IVectorImageService? _vectorService = null;

        public IImageSource Source => _source;

        public void Dispose()
        {
            _connection?.Dispose();
            _connection = null;
            _sourceStream?.Dispose();
            _sourceStream = null;
            _bitmapDecoder = null;
            _vectorService?.Dispose();
            _vectorService = null;
        }

        public async Task<string> GetFingerprint()
        {
            if (_fingerprint is not null)
            {
                return _fingerprint;
            }

            IImageConnection? connection = await GetConnection();
            if (connection is null)
            {
                return string.Empty;
            }

            _fingerprint = connection.Fingerprint;
            return _fingerprint;
        }

        public async Task<Stream?> GetSourceStream()
        {
            if (_sourceStream is not null)
            {
                _sourceStream.Seek(0, SeekOrigin.Begin);
                return _sourceStream;
            }

            IImageConnection? connection = await GetConnection();
            if (connection is null)
            {
                return null;
            }

            try
            {
                _sourceStream = await connection.OpenImageStream();
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

        public async Task<BitmapDecoder?> GetBitmapDecoder()
        {
            if (_bitmapDecoder is not null)
            {
                return _bitmapDecoder;
            }

            Stream? stream = await GetSourceStream();
            if (stream is null)
            {
                return null;
            }

            try
            {
                _bitmapDecoder = await BitmapDecoder.CreateAsync(stream.AsRandomAccessStream());
            }
            catch (Exception ex)
            {
                Logger.E(TAG, ex);
            }

            return _bitmapDecoder;
        }

        public async Task<IVectorImageService?> GetVectorService()
        {
            if (_vectorService is not null)
            {
                return _vectorService;
            }

            IImageConnection? connection = await GetConnection();
            if (connection is null)
            {
                return null;
            }

            _vectorService = connection.OpenVectorService();
            return _vectorService;
        }

        private async Task<IImageConnection?> GetConnection()
        {
            if (!_connectionInitialized)
            {
                _connection = await _source.Open();
                _connectionInitialized = true;
            }

            return _connection;
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
}
