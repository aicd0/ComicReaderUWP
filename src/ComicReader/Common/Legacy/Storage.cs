// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using ComicReader.SDK.Common.DebugTools;

using Windows.Storage;

namespace ComicReader.Common.Legacy;

internal static class Storage
{
    public static async Task<StorageFolder?> TryGetFolder(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        try
        {
            return await StorageFolder.GetFolderFromPathAsync(path);
        }
        catch (Exception ex)
        {
            Logger.E("Storage", "TryGetFolder", ex);
        }
        return null;
    }

    public static async Task<StorageFile?> TryGetFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        try
        {
            return await StorageFile.GetFileFromPathAsync(path);
        }
        catch (Exception ex)
        {
            Logger.E("Storage", "TryGetFolder", ex);
        }
        return null;
    }

    public static async Task<StorageFile?> TryGetFile(StorageFolder folder, string name)
    {
        IStorageItem item = await folder.TryGetItemAsync(name);

        if (item == null)
        {
            return null;
        }

        if (!item.IsOfType(StorageItemTypes.File))
        {
            return null;
        }

        return (StorageFile)item;
    }
}
