// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using ComicReaderUWP.Common.ErrorHandling;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Storage;

namespace ComicReaderUWP.Common.Utils;

internal static class ThirdPartyLauncher
{
    private const string TAG = nameof(ThirdPartyLauncher);

    public static ErrorResult<bool> ShowInFileExplorer(string path)
    {
        var err = ErrorLogger<bool>.Create(TAG);

        if (string.IsNullOrEmpty(path))
        {
            return err.SetError("Path is empty.", isFatal: true);
        }

        if (File.Exists(path))
        {
            ErrorResult<bool> innerErr = StartProcess("explorer.exe", $"/select,\"{path}\"");
            if (!innerErr.IsSuccessful)
            {
                return err.SetError(innerErr);
            }
        }
        else if (Directory.Exists(path))
        {
            ErrorResult<bool> innerErr = StartProcess("explorer.exe", $"\"{path}\"");
            if (!innerErr.IsSuccessful)
            {
                return err.SetError(innerErr);
            }
        }
        else
        {
            return err.SetError($"Path does not exist: {path}");
        }

        return err.SetResult(default);
    }

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

    private static ErrorResult<bool> StartProcess(string fileName, string arguments)
    {
        var err = ErrorLogger<bool>.Create(TAG);

        try
        {
            Process.Start(fileName, arguments);
        }
        catch (Win32Exception ex)
        {
            return err.SetError(ex.Message);
        }
        catch (Exception ex)
        {
            return err.SetError(ex, isFatal: true);
        }

        return err.SetResult(default);
    }
}
