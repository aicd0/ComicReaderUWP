// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using ComicReaderUWP.Core.Common.ErrorHandling;
using ComicReaderUWP.Core.Common.Storage;
using ComicReaderUWP.Core.Common.Threading;

using Windows.Storage;
using Windows.System;

namespace ComicReaderUWP.Common.Utils;

internal static class ThirdPartyLauncher
{
    private const string TAG = nameof(ThirdPartyLauncher);

    public static async Task<OperationResult> ShowInFileExplorer(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return OperationResultExt.Failure(TAG, "Path is empty.", isFatal: true);
        }

        try
        {
            if (File.Exists(path))
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(path);
                StorageFolder folder = await file.GetParentAsync();
                FolderLauncherOptions options = new();
                options.ItemsToSelect.Add(file);
                await Launcher.LaunchFolderAsync(folder, options);
            }
            else if (Directory.Exists(path))
            {
                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(path);
                await Launcher.LaunchFolderAsync(folder);
            }
            else
            {
                return OperationResultExt.Failure(TAG, $"Path does not exist: {path}");
            }
        }
        catch (Exception ex)
        {
            return OperationResultExt.Failure(TAG, ex);
        }

        return OperationResultExt.Success();
    }

    public static async Task<OperationResult<string>> OpenTemporaryTextFile(string filename, string text)
    {
        OperationResult<string> writeErr = WriteTemporaryTextFile(filename, text);
        if (!writeErr.IsSuccessful)
        {
            return OperationResult<string>.Failure(writeErr);
        }

        OperationResult openErr = await OpenFile(writeErr.Result);
        if (!openErr.IsSuccessful)
        {
            return OperationResult<string>.Failure(openErr);
        }

        return OperationResult<string>.Success(writeErr.Result);
    }

    public static async Task<OperationResult> OpenAssetsFile(string filename)
    {
        return await OpenFile(Path.Combine(AppContext.BaseDirectory, "Assets", filename));
    }

    public static async Task<OperationResult> OpenFile(string path)
    {
        return await StartProcess(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        }, waitForExit: false);
    }

    public static async Task<OperationResult<string>> EditTemporaryTextFileAsync(string filename, IEnumerable<string> lines)
    {
        string text = string.Join(Environment.NewLine, lines);
        OperationResult<string> writeErr = WriteTemporaryTextFile(filename, text);
        if (!writeErr.IsSuccessful)
        {
            return OperationResultExt<string>.Failure(writeErr);
        }

        string filePath = writeErr.Result;
        OperationResult started = await StartProcess(new ProcessStartInfo
        {
            FileName = "notepad.exe",
            Arguments = $"\"{filePath}\"",
            UseShellExecute = false,
        }, waitForExit: true);

        if (!started.IsSuccessful)
        {
            return OperationResultExt<string>.Failure(started);
        }

        return OperationResultExt<string>.Success(filePath);
    }

    private static OperationResult<string> WriteTemporaryTextFile(string filename, string text)
    {
        string directoryPath = StorageLocation.TemporaryFolderPath;
        try
        {
            Directory.CreateDirectory(directoryPath);
        }
        catch (Exception ex)
        {
            return OperationResultExt<string>.Failure(TAG, ex);
        }

        string filePath = Path.Combine(directoryPath, filename);
        try
        {
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            writer.Write(text);
        }
        catch (Exception ex)
        {
            return OperationResultExt<string>.Failure(TAG, ex);
        }

        return OperationResultExt<string>.Success(filePath);
    }

    private static Task<OperationResult> StartProcess(ProcessStartInfo info, bool waitForExit)
    {
        return MainThreadUtils.PostInMainThread(async () =>
        {
            try
            {
                using var process = Process.Start(info);
                if (process is null)
                {
                    return OperationResultExt.Failure(TAG, "Failed to launch notepad.");
                }

                if (waitForExit)
                {
                    await process.WaitForExitAsync();
                }
            }
            catch (Exception ex)
            {
                return OperationResultExt.Failure(TAG, ex);
            }

            return OperationResultExt.Success();
        });
    }
}
