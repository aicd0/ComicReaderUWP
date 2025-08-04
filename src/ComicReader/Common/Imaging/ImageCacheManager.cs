// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

using ComicReader.Common.Threading;
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
    private const string CACHE_FOLDER = "images";
    private const long MAX_CACHE_SIZE = 1024 * 1024 * 1024;

    private static readonly object sLock = new();
    private static volatile LRUCache sImageCache;

    private static int sPostMainThreadTask = 0;
    private static readonly ConcurrentQueue<RenderItem> sRenderQueue = new();

    public static void LoadImage(CancellationSession.IToken token,
        IImageSource source, double frameWidth, double frameHeight, StretchModeEnum stretchMode,
        IImageResultHandler handler)
    {
        Logger.Assert(!MainThreadUtils.IsMainThread(), "AD0290621DDD0E4F");

        if (token.IsCancellationRequested)
        {
            return;
        }

        long startTime = GetCurrentTick();
        string uri = source.GetUri();
        int sourceSignature = source.GetContentSignature();

        if (uri == null || uri.Length == 0)
        {
            Logger.AssertNotReachHere("0DA4B46E1643F4BF");
            return;
        }

        LRUCache imageCache = GetImageLRUCache();
        IRandomAccessStream thumbnailStream = null;
        bool requireThumbnail = true;
        ImageCacheDatabase.CacheRecord cacheRecord = ImageCacheDatabase.GetCacheRecord(source);
        if (imageCache != null && cacheRecord != null)
        {
            requireThumbnail = false;
            CalculateDesiredDimension(frameWidth, frameHeight, stretchMode, cacheRecord.Width, cacheRecord.Height, out int desiredWidth, out int desiredHeight);
            IEnumerable<string> cacheEntryKeys = ImageCacheStrategy.CalculateCacheEntryKeys(desiredWidth, desiredHeight, cacheRecord.Width, cacheRecord.Height);
            foreach (string cacheEntryKey in cacheEntryKeys)
            {
                requireThumbnail = true;
                string entry = cacheRecord.GetEntry(cacheEntryKey);
                if (entry.Length > 0)
                {
                    thumbnailStream = imageCache.Get(entry);
                }
                if (thumbnailStream != null)
                {
                    break;
                }
            }
        }

        IRandomAccessStream sourceStream = null;
        if (thumbnailStream == null && requireThumbnail)
        {
            sourceStream = TryOpenImageStreamAsync(source).Result;
            if (sourceStream == null)
            {
                Logger.E(TAG, "sourceStream is null");
                return;
            }

            try
            {
                thumbnailStream = TryCreateThumbnail(cacheRecord, sourceStream, frameWidth, frameHeight, stretchMode, uri, sourceSignature);
            }
            catch (Exception)
            {
                sourceStream.Dispose();
                throw;
            }
        }

        var item = new RenderItem
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
        sRenderQueue.Enqueue(item);
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
            // Only responsible for rendering tasks that have been queued before this point.
            while (sRenderQueue.TryDequeue(out RenderItem item))
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
        BitmapImage image = null;
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

    private static IRandomAccessStream TryCreateThumbnail(ImageCacheDatabase.CacheRecord cacheRecord, IRandomAccessStream sourceStream,
        double frameWidth, double frameHeight, StretchModeEnum stretchMode, string cacheKey, int sourceSignature)
    {
        LRUCache imageCache = GetImageLRUCache();
        if (imageCache == null)
        {
            return null;
        }

        sourceStream.Seek(0);
        Image image = null;
        try
        {
            image = Image.Load(sourceStream.AsStream());
        }
        catch (Exception ex)
        {
            Logger.F(TAG, "TryCreateThumbnail", ex);
        }
        if (image == null)
        {
            return null;
        }

        try
        {
            int sourceWidth = image.Width;
            int sourceHeight = image.Height;
            CalculateDesiredDimension(frameWidth, frameHeight, stretchMode, sourceWidth, sourceHeight, out int desiredWidth, out int desiredHeight);
            IEnumerable<string> cacheEntryKeys = ImageCacheStrategy.CalculateCacheEntryKeys(desiredWidth, desiredHeight, sourceWidth, sourceHeight);
            string cacheEntryKey = null;
            foreach (string key in cacheEntryKeys)
            {
                cacheEntryKey = key;
                break;
            }
            MemoryStream cacheStream = CreateImageCacheStream(cacheEntryKey, sourceWidth, sourceHeight, image);

            try
            {
                string entry = null;
                if (cacheStream != null)
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

                if (cacheRecord != null)
                {
                    cacheRecord.UpdateMeta(sourceSignature, sourceWidth, sourceHeight);
                }
                else
                {
                    cacheRecord = new ImageCacheDatabase.CacheRecord(cacheKey, sourceSignature, sourceWidth, sourceHeight);
                }

                if (entry != null)
                {
                    cacheRecord.PutEntry(cacheEntryKey, entry);
                }

                cacheRecord.Save();
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

    private static MemoryStream CreateImageCacheStream(string cacheEntryKey, int sourceWidth, int sourceHeight, Image image)
    {
        if (cacheEntryKey == null || cacheEntryKey.Length == 0)
        {
            return null;
        }

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

        MemoryStream memoryStream = new();
        try
        {
            image.Mutate(x => x.Resize(aspectWidth, aspectHeight));
            image.Save(memoryStream, image.Metadata.DecodedImageFormat);
        }
        catch (Exception e)
        {
            Logger.F(TAG, "CreateImageCacheStream", e);
            memoryStream.Dispose();
            memoryStream = null;
        }

        return memoryStream;
    }

    private static async Task<IRandomAccessStream> TryOpenImageStreamAsync(IImageSource source)
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

    private static async Task<BitmapImage> TryLoadImageFromStreamAsync(IRandomAccessStream stream)
    {
        BitmapImage image = new();

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

    private static LRUCache GetImageLRUCache()
    {
        {
            LRUCache cache = sImageCache;
            if (cache != null)
            {
                return cache;
            }
        }

        lock (sLock)
        {
            LRUCache cache = sImageCache;
            if (cache != null)
            {
                return cache;
            }

            string cacheFolderPath = Path.Combine(StorageLocation.LocalCacheFolderPath, CACHE_FOLDER);
            try
            {
                Directory.CreateDirectory(cacheFolderPath);
            }
            catch (Exception e)
            {
                Logger.AssertNotReachHere("266A35BFD509B7A0", e);
                return null;
            }
            cache = new(cacheFolderPath, MAX_CACHE_SIZE);
            sImageCache = cache;
            TaskDispatcher.LongRunningThreadPool.Submit("CleanImageCache", delegate
            {
                cache.Clean();
            });
            return cache;
        }
    }

    private static long GetCurrentTick()
    {
        return Environment.TickCount64;
    }

    private class RenderItem
    {
        public CancellationSession.IToken Token;
        public IImageSource Source;
        public string Uri;
        public double FrameWidth;
        public double FrameHeight;
        public StretchModeEnum StretchMode;
        public IRandomAccessStream ThumbnailStream;
        public IRandomAccessStream SourceStream;
        public IImageResultHandler Handler;
        public long StartTime;
    }
}
