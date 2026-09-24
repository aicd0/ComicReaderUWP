// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

using ComicReaderUWP.Helpers.MenuFlyoutHelpers;

namespace ComicReaderUWP.Views.Pages.Reader;

internal partial class ReaderPreviewImageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public Func<Task<IReadOnlyList<BaseMenuFlyoutItemModel>>>? RequestContextMenu { get; init; }

    private string? _imageUri = null;
    public string? ImageUri
    {
        get => _imageUri;
        set
        {
            _imageUri = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageUri)));
        }
    }

    public double ImageWidth { get; init; } = double.PositiveInfinity;

    public double ImageHeight { get; init; } = double.PositiveInfinity;

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
