// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using ComicReaderUWP.Common.ErrorHandling;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Storage;

namespace ComicReaderUWP.Common.Utils;

internal class ThirdPartyLauncher
{
    private const string TAG = nameof(ThirdPartyLauncher);

    public static void StartTemporaryTextFile(string filename, string text)
    {
        ErrorResult<string> writeErr = WriteTemporaryTextFile(filename, text);
        if (!writeErr.IsSuccessful)
        {
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = writeErr.Result,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Logger.F(TAG, nameof(StartTemporaryTextFile), ex);
        }
    }

    public static async Task<ErrorResult<string>> EditTemporaryTextFileAsync(string filename, IEnumerable<string> lines)
    {
        var err = ErrorLogger<string>.Create(TAG);

        string text = string.Join(Environment.NewLine, lines);
        ErrorResult<string> writeErr = WriteTemporaryTextFile(filename, text);
        if (!writeErr.IsSuccessful)
        {
            return err.SetError(writeErr);
        }

        string filePath = writeErr.Result;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{filePath}\"",
                UseShellExecute = false,
            };
            using var process = Process.Start(psi);
            if (process is null)
            {
                return err.SetError("Failed to launch notepad.");
            }

            await process.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            return err.SetError(ex);
        }

        return err.SetResult(filePath);
    }

    private static ErrorResult<string> WriteTemporaryTextFile(string filename, string text)
    {
        var err = ErrorLogger<string>.Create(TAG);

        string directoryPath = StorageLocation.TemporaryFolderPath;
        try
        {
            Directory.CreateDirectory(directoryPath);
        }
        catch (Exception ex)
        {
            return err.SetError(ex);
        }

        string filePath = Path.Combine(directoryPath, filename);
        try
        {
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            writer.Write(text);
        }
        catch (Exception ex)
        {
            return err.SetError(ex);
        }

        return err.SetResult(filePath);
    }
}
