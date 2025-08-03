// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.ComponentModel;

namespace ComicReader.Views.Pages.Navigation;

public class NavigationPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    private bool _devToolsVisible;
    public bool DevToolsVisible
    {
        get => _devToolsVisible;
        set
        {
            _devToolsVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DevToolsVisible)));
        }
    }
}
