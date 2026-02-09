// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.ComponentModel;

using ComicReaderUWP.Common.Imaging;

namespace ComicReaderUWP.ViewModels;

internal partial class ReaderImagePreviewViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    private SimpleImageView.Model _image;
    public SimpleImageView.Model Image
    {
        get => _image;
        set
        {
            _image = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Image)));
        }
    }

    private int _page = -1;
    public int Page
    {
        get => _page;
        set
        {
            _page = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Page)));
        }
    }

    private bool _selected = false;
    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
        }
    }
}
