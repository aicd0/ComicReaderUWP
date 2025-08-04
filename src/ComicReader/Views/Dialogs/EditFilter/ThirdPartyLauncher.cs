// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;

using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Storage;

namespace ComicReader.Views.Dialogs.EditFilter;

internal class ThirdPartyLauncher
{
    private const string TAG = nameof(ThirdPartyLauncher);

    public static void StartTemporaryTextFile(string filename, string text)
    {
        string directoryPath = StorageLocation.TemporaryFolderPath;
        try
        {
            Directory.CreateDirectory(directoryPath);
        }
        catch (Exception ex)
        {
            Logger.F(TAG, nameof(StartTemporaryTextFile), ex);
            return;
        }

        string filePath = Path.Combine(directoryPath, filename);
        try
        {
            using StreamWriter stream = File.CreateText(filePath);
            stream.Write(text);
        }
        catch (Exception ex)
        {
            Logger.F(TAG, nameof(StartTemporaryTextFile), ex);
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Logger.F(TAG, nameof(StartTemporaryTextFile), ex);
        }
    }
}
