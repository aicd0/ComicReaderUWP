// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;

using ComicReader.Common.Imaging;

using Microsoft.UI.Xaml.Media.Imaging;

namespace ComicReader.Views.Pages.Reader;

internal class ImageHolder
{
    private readonly ReaderImagePool _pool;
    private readonly Action<BitmapImage> _setter;

    private IImageSource _currentImageSource;
    private string _currentUri = string.Empty;
    private BitmapImage _currentImage;

    public ImageHolder(ReaderImagePool pool, Action<BitmapImage> setter)
    {
        _pool = pool;
        _setter = setter;
    }

    public void SetImage(IImageSource source)
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
            _pool.RecycleImage(_currentImageSource, _currentImage);
        }

        _currentImageSource = source;
        _currentImage = null;

        if (uri.Length == 0)
        {
            _setter(null);
            return;
        }

        _pool.RequestImage(source, OnImageCallback);
    }

    private void OnImageCallback(BitmapImage image)
    {
        if (image == null)
        {
            _currentImageSource = null;
            _currentUri = string.Empty;
        }

        _currentImage = image;
        _setter(image);
    }
}
