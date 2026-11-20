// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.ErrorHandling;
using ComicReaderUWP.Core.Common.Storage;

using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReaderUWP.Common.Utils;

internal static class ClipboardUtils
{
    private const string TAG = nameof(ClipboardUtils);
    private const string TEMP_FOLDER_NAME = "Clipboard";

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

    public static async Task<OperationResult> SetImage(Stream stream)
    {
        try
        {
            stream.Seek(0, SeekOrigin.Begin);

            // The clipboard broker reads bitmap data lazily, often on a background
            // thread or out of process. The AsRandomAccessStream() adapter over a
            // managed Stream is not reliable in that scenario (it can silently leave
            // the clipboard empty), so fully buffer the image into an
            // InMemoryRandomAccessStream first and keep the buffer alive.
            var buffer = new InMemoryRandomAccessStream();
            IRandomAccessStream input = stream.AsRandomAccessStream();
            await RandomAccessStream.CopyAsync(input, buffer);
            buffer.Seek(0);

            DataPackage dataPackage = new()
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(buffer));

            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();
        }
        catch (Exception ex)
        {
            return OperationResultExt.Failure(nameof(SetImage), ex);
        }

        return OperationResultExt.Success();
    }

    public static async Task<OperationResult> SetFile(string path)
    {
        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(path);
            return SetFile(file);
        }
        catch (Exception ex)
        {
            return OperationResultExt.Failure(nameof(SetFile), ex);
        }
    }

    public static async Task<OperationResult> SetFile(Stream stream, string filename)
    {
        string tempFilePath;
        try
        {
            string directoryPath = Path.Combine(StorageLocation.TemporaryFolderPath, TEMP_FOLDER_NAME);
            Directory.CreateDirectory(directoryPath);

            tempFilePath = Path.Combine(directoryPath, filename);
            using FileStream fileStream = new(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fileStream);
        }
        catch (Exception ex)
        {
            return OperationResultExt.Failure(nameof(SetFile), "Failed to write the temporary file.", ex);
        }

        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(tempFilePath);
            return SetFile(file);
        }
        catch (Exception ex)
        {
            return OperationResultExt.Failure(nameof(SetFile), ex);
        }
    }

    private static OperationResult SetFile(StorageFile file)
    {
        try
        {
            DataPackage dataPackage = new()
            {
                RequestedOperation = DataPackageOperation.Copy,
            };
            dataPackage.SetStorageItems(new List<IStorageItem> { file });

            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();
        }
        catch (Exception ex)
        {
            return OperationResultExt.Failure(nameof(SetFile), ex);
        }

        return OperationResultExt.Success();
    }
}
