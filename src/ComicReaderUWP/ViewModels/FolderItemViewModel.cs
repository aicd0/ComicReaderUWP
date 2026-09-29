// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.BaseUI;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace ComicReaderUWP.ViewModels;

public class FolderItemViewModel : BaseViewModel
{
    public string Folder { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsAddNew { get; set; }

    // events
    public TappedEventHandler? OnItemTapped { get; set; }
    public RoutedEventHandler? OnRemoveClicked { get; set; }
}
