// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;

using ComicReader.Common.BaseUI;

namespace ComicReader.ViewModels;

internal partial class LinkItemViewModel : BaseViewModel, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isPlaceholder = false;
    public bool IsPlaceholder
    {
        get => _isPlaceholder;
        set
        {
            _isPlaceholder = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPlaceholder)));
        }
    }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    private string _link = string.Empty;
    public string Link
    {
        get => _link;
        set
        {
            _link = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Link)));
        }
    }

    private bool _global = false;
    public bool Global
    {
        get => _global;
        set
        {
            _global = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Global)));
        }
    }
}
