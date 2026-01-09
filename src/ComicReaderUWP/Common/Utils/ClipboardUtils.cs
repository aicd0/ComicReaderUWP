// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReaderUWP.SDK.Common.DebugTools;

using Windows.ApplicationModel.DataTransfer;

namespace ComicReaderUWP.Common.Utils;

internal static class ClipboardUtils
{
    private const string TAG = nameof(ClipboardUtils);

    public static void SetText(string text)
    {
        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();
        }
        catch (Exception ex)
        {
            Logger.E(TAG, "Failed to set clipboard text.", ex);
        }
    }
}
