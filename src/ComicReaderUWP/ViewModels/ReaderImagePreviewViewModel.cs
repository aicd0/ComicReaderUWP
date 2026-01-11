// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.ComponentModel;

using ComicReaderUWP.Common.Imaging;

namespace ComicReaderUWP.ViewModels;

internal partial class ReaderImagePreviewViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    private SimpleImageView.Model _Image;
    public SimpleImageView.Model Image
    {
        get => _Image;
        set
        {
            _Image = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Image)));
        }
    }

    private int _Page = -1;
    public int Page
    {
        get => _Page;
        set
        {
            _Page = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Page)));
        }
    }
}
