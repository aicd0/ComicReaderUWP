// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;

using ComicReaderUWP.Common.BaseUI;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace ComicReaderUWP.ViewModels;

public class FolderItemViewModel : BaseViewModel
{
    public string Folder { get; set; }
    public string Path { get; set; }
    public bool IsAddNew { get; set; }

    // events
    public TappedEventHandler OnItemTapped { get; set; }
    public RoutedEventHandler OnRemoveClicked { get; set; }

    // methods
    public static Func<FolderItemViewModel, FolderItemViewModel, bool> ContentEquals = delegate (FolderItemViewModel a, FolderItemViewModel b)
    {
        if (a.IsAddNew != b.IsAddNew)
        {
            return false;
        }

        if (a.IsAddNew)
        {
            return true;
        }

        return a.Path == b.Path;
    };
}
