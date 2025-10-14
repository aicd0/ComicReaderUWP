// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

using ComicReader.Common.BaseUI;
using ComicReader.Common.Legacy;
using ComicReader.Common.Utils;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using Windows.Storage;

namespace ComicReader.Views.Dialogs.ChooseLocation;

public sealed partial class ChooseLocationsDialog : BaseContentDialog
{
    public ObservableCollection<FolderItemViewModel> FolderItemDataSource { get; set; }

    private int WindowId { get; }

    public ChooseLocationsDialog(int windowId)
    {
        InitializeComponent();

        FolderItemDataSource = [];
        WindowId = windowId;
    }

    private void Update()
    {
        FolderItemDataSource.Clear();

        FolderItemDataSource.Add(new FolderItemViewModel
        {
            IsAddNew = true
        });

        foreach (string folder in AppSettingsModel.Instance.GetModel().ComicFolders)
        {
            FolderItemDataSource.Add(new FolderItemViewModel
            {
                Folder = folder,
                IsAddNew = false
            });
        }
    }

    private void ContentDialogPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ComicModel.UpdateAllComics("ContentDialogPrimaryButtonClick");
    }

    private void ListViewLoaded(object sender, RoutedEventArgs e)
    {
        Update();
    }

    private void AddNewPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        C0.Run(async delegate
        {
            if (!IsPrimaryButtonEnabled)
            {
                return;
            }
            IsPrimaryButtonEnabled = false;
            try
            {
                StorageFolder? folder = await FilePickerUtils.PickFolder(WindowId);
                if (folder == null)
                {
                    return;
                }
                AppSettingsModel.Instance.AddComicFolder(folder.Path);
                Update();
            }
            finally
            {
                IsPrimaryButtonEnabled = true;
            }
        });
    }

    private void RemoveFolderPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!IsPrimaryButtonEnabled)
        {
            return;
        }
        IsPrimaryButtonEnabled = false;
        try
        {
            var item = (FolderItemViewModel)((Grid)sender).DataContext;
            AppSettingsModel.Instance.RemoveComicFolder(item.Folder);
            Update();
        }
        finally
        {
            IsPrimaryButtonEnabled = true;
        }
    }
}
