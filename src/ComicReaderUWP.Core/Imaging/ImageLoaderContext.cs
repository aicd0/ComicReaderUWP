// Copyright (c) aicd0. All rights reserved.
// Licensed under the GNU General Public License v3.0 or later.

using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Utils;

using Windows.Graphics.Imaging;

namespace ComicReaderUWP.Core.Imaging;

public partial class ImageLoaderContext(IImageSource source) : IDisposable
{
    private const string TAG = nameof(ImageLoaderContext);

    private const int VALIDATION_PRIORITY = -1;
    private const int IMAGE_META_VERSION = 1;

    private static ImageCacheDatabase? sDatabase = null;

    internal static void InjectDatabase(ImageCacheDatabase database)
    {
        sDatabase = database;
    }

    internal static bool ValidateCacheRecord(ImageCacheDatabase.CacheRecord record, string? fingerprint)
    {
        string metaVersion = record.GetExt(ImageCacheExt.IMAGE_META_VERSION) ?? string.Empty;
        if (metaVersion != IMAGE_META_VERSION.ToString())
        {
            return false;
        }

        string metaFingerprint = record.GetExt(ImageCacheExt.IMAGE_META_FINGERPRINT) ?? string.Empty;
        return fingerprint is null || metaFingerprint == fingerprint;
    }

    internal static void SaveImageMetaToCacheRecord(ImageCacheDatabase.CacheRecord record, string sourceFingerprint, long size, BitmapDecoder decoder)
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

    private readonly IImageSource _source = source;

    private bool _connectionInitialized = false;
    private IImageConnection? _connection = null;

    private string? _cacheKey = null;
    private string? _fingerprint = null;
    private Stream? _sourceStream = null;
    private BitmapDecoder? _bitmapDecoder = null;
    private IVectorImageService? _vectorService = null;

    private int _disposeRequested = 0;
    private readonly Lock _lock = new();
    private int _referenceCount = 1;
    private ValidationState _validation = ValidationState.NotValidated;

    internal IImageSource Source => _source;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeRequested, 1) != 0)
        {
            return;
        }

        Release();
    }

    internal void Acquire()
    {
        lock (_lock)
        {
            if (_referenceCount > 0)
            {
                _referenceCount++;
            }
            else
            {
                throw new ObjectDisposedException(nameof(ImageLoaderContext));
            }
        }
    }

    internal void Release()
    {
        lock (_lock)
        {
            if (--_referenceCount > 0)
            {
                return;
            }
        }

        _connection?.Dispose();
        _connection = null;
        _sourceStream?.Dispose();
        _sourceStream = null;
        _bitmapDecoder = null;
        _vectorService?.Dispose();
        _vectorService = null;
    }

    internal async Task<ImageCacheDatabase.CacheRecord?> GetOrCreateCache()
    {
        ImageCacheDatabase? db = sDatabase ?? throw new InvalidOperationException("No cache database was injected into this context.");

        string uri = _source.Uri;
        if (string.IsNullOrEmpty(uri))
        {
            Logger.E(TAG, "Image source URI is null or empty");
            return null;
        }

        ImageCacheDatabase.CacheRecord? cache = db.GetCache(uri);
        if (cache is not null)
        {
            return cache;
        }

        string cacheKey = await GetCacheKey(db);
        if (string.IsNullOrEmpty(cacheKey))
        {
            Logger.E(TAG, "Image cache key is null or empty");
            return null;
        }

        return db.GetOrCreateCache(cacheKey);
    }

    internal async Task<string?> GetValidationFingerprint()
    {
        switch (_source.CacheValidationMode)
        {
            case ImageCacheValidationMode.None:
                return null;

            case ImageCacheValidationMode.Deferred:
                TryBeginDeferredValidation();
                return null;

            default:
                return await GetFingerprint();
        }
    }

    internal async Task<string> GetFingerprint()
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

    internal async Task<Stream?> GetSourceStream()
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

    internal void UnrefSourceStream()
    {
        _sourceStream = null;
        _bitmapDecoder = null;
    }

    internal async Task<BitmapDecoder?> GetBitmapDecoder()
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

    internal async Task<IVectorImageService?> GetVectorService()
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

    private async Task<string> GetCacheKey(ImageCacheDatabase db)
    {
        if (_cacheKey is not null)
        {
            return _cacheKey;
        }

        _cacheKey = string.Empty;

        string uri = _source.Uri;
        if (string.IsNullOrEmpty(uri))
        {
            Logger.E(TAG, "Image source URI is null or empty");
            return _cacheKey;
        }

        if (_source.CacheValidationMode != ImageCacheValidationMode.Synchronized)
        {
            string? knownCacheKey = db.GetCacheKey(uri);
            if (!string.IsNullOrEmpty(knownCacheKey))
            {
                _cacheKey = knownCacheKey;
                return _cacheKey;
            }
        }

        IImageConnection? connection = await GetConnection();
        if (connection is null)
        {
            return _cacheKey;
        }

        string cacheKey = connection.CacheKey;
        if (string.IsNullOrEmpty(cacheKey))
        {
            Logger.E(TAG, $"Image connection cache key is null or empty (uri={uri})");
            return _cacheKey;
        }

        _cacheKey = cacheKey;
        if (uri != cacheKey)
        {
            db.SetCacheKey(uri, cacheKey);
        }

        return _cacheKey;
    }

    private void TryBeginDeferredValidation()
    {
        ImageCacheDatabase? db = sDatabase ?? throw new InvalidOperationException("No cache database was injected into this context.");

        lock (_lock)
        {
            if (_validation != ValidationState.NotValidated || _referenceCount <= 0)
            {
                return;
            }

            _validation = ValidationState.Validating;
            _referenceCount++;
        }

        CoroutineUtils.Run(async () =>
        {
            try
            {
                ImageLoaderSchedulerGroup group = _source.PreferredSchedulerGroup;
                await ImageLoaderScheduler.Submit(ValidateCache, group, VALIDATION_PRIORITY);
            }
            finally
            {
                Release();
            }
        });
    }

    private async Task ValidateCache()
    {
        bool? stale = null;

        string fingerprint = await GetFingerprint();
        if (fingerprint.Length > 0)
        {
            ImageCacheDatabase.CacheRecord? record = await GetOrCreateCache();
            if (record is not null)
            {
                stale = await record.Enqueue(() =>
                {
                    if (string.IsNullOrEmpty(record.GetExt(ImageCacheExt.IMAGE_META_VERSION)))
                    {
                        return Task.FromResult<bool?>(null);
                    }

                    if (ValidateCacheRecord(record, fingerprint))
                    {
                        return Task.FromResult<bool?>(false);
                    }

                    record.Clear();
                    record.Save();
                    return Task.FromResult<bool?>(true);
                });

                if (stale == true)
                {
                    Logger.I(TAG, $"The picture of '{_source.Uri}' no longer matches the file and its cache was dropped");
                }
            }
        }

        lock (_lock)
        {
            _validation = stale is null ? ValidationState.NotValidated : ValidationState.Validated;
        }
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

    private enum ValidationState
    {
        NotValidated,
        Validating,
        Validated,
    }
}