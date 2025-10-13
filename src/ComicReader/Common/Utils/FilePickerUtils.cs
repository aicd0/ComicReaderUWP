// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using ComicReader.Views.AppWindows.Main;

using Windows.Storage;
using Windows.Storage.Pickers;

namespace ComicReader.Common.Utils;

internal static class FilePickerUtils
{
    public static Task<StorageFolder?> PickFolder(int windowId)
    {
        MainWindow? window = App.WindowManager.GetWindow(windowId);
        if (window is null)
        {
            return Task.FromResult<StorageFolder?>(null);
        }
        FolderPicker picker = InitializeWithWindow(new FolderPicker(), window.WindowHandle);
        picker.FileTypeFilter.Add("*");
        return picker.PickSingleFolderAsync().AsTask();
    }

    private static FolderPicker InitializeWithWindow(FolderPicker obj, nint windowHandle)
    {
        WinRT.Interop.InitializeWithWindow.Initialize(obj, windowHandle);
        return obj;
    }
}
