// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using ComicReaderUWP.Core.Common.DebugTools;

using Windows.Storage;

namespace ComicReaderUWP.Common.Legacy;

internal static class Storage
{
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
}
