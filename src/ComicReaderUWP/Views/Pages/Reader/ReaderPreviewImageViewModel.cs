// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Helpers.MenuFlyoutHelpers;

namespace ComicReaderUWP.Views.Pages.Reader;

internal partial class ReaderPreviewImageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public Func<Task<IReadOnlyList<BaseMenuFlyoutItemModel>>>? RequestContextMenu { get; init; }

    private SimpleImageView.Model? _image;
    public SimpleImageView.Model? Image
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
