// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReaderUWP.Common.Imaging;

namespace ComicReaderUWP.UserControls.Reader;

internal partial class ImageHolder : IDisposable
{
    private readonly ReaderImagePool? _pool;
    private readonly Action<DecodedImageModel?> _setter;

    private IImageSource? _currentImageSource;
    private string _currentUri = string.Empty;
    private DecodedImageModel? _currentImage;

    public ImageHolder(ReaderImagePool pool, Action<DecodedImageModel?> setter)
    {
        _pool = pool;
        _setter = setter;
    }

    public void Dispose()
    {
        _currentImage?.Dispose();
        _currentImage = null;
    }

    public void SetImage(IImageSource? source)
    {
        if (_pool == null)
        {
            return;
        }

        string uri;
        if (source == null)
        {
            uri = string.Empty;
        }
        else
        {
            uri = source.GetUri() ?? string.Empty;
        }

        if (_currentUri == uri)
        {
            return;
        }

        _currentUri = uri;
        _pool.CancelRequest(OnImageCallback);

        if (_currentImage != null && _currentImageSource != null)
        {
            DecodedImageModel recyclingImage = _currentImage;
            _currentImage = null;
            _pool.RecycleImage(_currentImageSource, recyclingImage);
        }
        else
        {
            _currentImage?.Dispose();
            _currentImage = null;
        }

        _currentImageSource = source;

        if (uri.Length == 0 || source is null)
        {
            _setter(null);
            return;
        }

        _pool.RequestImage(source, OnImageCallback);
    }

    private void OnImageCallback(DecodedImageModel? image)
    {
        if (image == null)
        {
            _currentImageSource = null;
            _currentUri = string.Empty;
        }

        _currentImage?.Dispose();
        _currentImage = image;
        _setter(image);
    }
}
