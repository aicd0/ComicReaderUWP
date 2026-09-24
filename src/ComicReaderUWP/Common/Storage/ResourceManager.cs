// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Storage;
using ComicReaderUWP.Core.Common.Threading;

namespace ComicReaderUWP.Common.Storage;

internal static class ResourceManager
{
    private const string TAG = nameof(ResourceManager);

    private const string RESOURCES_FOLDER_NAME = "Resources";
    private const int MAX_EXTENSION_LENGTH = 16;

    public static string ResourceRootPath => Path.Combine(StorageLocation.LocalFolderPath, RESOURCES_FOLDER_NAME);

    public static string Acquire()
    {
        return CreateResourceId();
    }

    public static Task Release(string resourceId)
    {
        return TaskDispatcher.LongRunningThreadPool.Submit(() =>
        {
            if (!IsSafeSegment(resourceId))
            {
                return;
            }

            string folderPath = GetResourceFolderPath(resourceId);
            try
            {
                Directory.Delete(folderPath, true);
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (Exception ex)
            {
                Logger.E(TAG, $"Failed to delete resource folder '{folderPath}'", ex);
            }
        });
    }

    public static async Task<string?> CreateFolder(string resourceId)
    {
        return await TaskDispatcher.LongRunningThreadPool.Submit(() =>
        {
            if (!IsSafeSegment(resourceId))
            {
                Logger.E(TAG, $"Invalid resource id '{resourceId}'");
                return null;
            }

            string folderPath = GetResourceFolderPath(resourceId);
            try
            {
                Directory.CreateDirectory(folderPath);
            }
            catch (Exception ex)
            {
                Logger.E(TAG, $"Failed to create resource folder '{folderPath}'", ex);
                return null;
            }

            return folderPath;
        });
    }

    public static bool TryGetFilePath(string resourceId, string fileName, [NotNullWhen(true)] out string? path)
    {
        if (!IsSafeSegment(resourceId) || !IsSafeSegment(fileName))
        {
            path = null;
            return false;
        }

        path = Path.Combine(GetResourceFolderPath(resourceId), fileName);
        return true;
    }

    public static async Task<string?> ImportFile(string resourceId, string sourceFilePath)
    {
        return await TaskDispatcher.LongRunningThreadPool.Submit(() =>
        {
            if (!IsSafeSegment(resourceId))
            {
                Logger.E(TAG, $"Invalid resource id '{resourceId}'");
                return null;
            }

            try
            {
                string folderPath = GetResourceFolderPath(resourceId);
                Directory.CreateDirectory(folderPath);

                string fileName = CreateResourceId() + GetSafeExtension(sourceFilePath);
                File.Copy(sourceFilePath, Path.Combine(folderPath, fileName), overwrite: false);
                return fileName;
            }
            catch (Exception ex)
            {
                Logger.E(TAG, $"Failed to import file '{sourceFilePath}'", ex);
                return null;
            }
        });
    }

    public static Task DeleteFile(string resourceId, string fileName)
    {
        return TaskDispatcher.LongRunningThreadPool.Submit(() =>
        {
            if (!TryGetFilePath(resourceId, fileName, out string? filePath))
            {
                return;
            }

            try
            {
                File.Delete(filePath);

                string? folderPath = Path.GetDirectoryName(filePath);
                if (folderPath is not null && Directory.Exists(folderPath) && Directory.GetFileSystemEntries(folderPath).Length == 0)
                {
                    Directory.Delete(folderPath);
                }
            }
            catch (Exception ex)
            {
                Logger.E(TAG, $"Failed to delete file '{filePath}'", ex);
            }
        });
    }

    private static string GetResourceFolderPath(string resourceId)
    {
        return Path.Combine(ResourceRootPath, resourceId);
    }

    private static string CreateResourceId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static bool IsSafeSegment(string segment)
    {
        if (string.IsNullOrEmpty(segment) || segment is "." or "..")
        {
            return false;
        }

        foreach (char c in segment)
        {
            if (c == '/' || c == '\\' || c == ':' || Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0)
            {
                return false;
            }
        }

        return true;
    }

    private static string GetSafeExtension(string filePath)
    {
        string extension = Path.GetExtension(filePath);
        if (extension.Length <= 1 || extension.Length > MAX_EXTENSION_LENGTH)
        {
            return string.Empty;
        }

        foreach (char c in extension)
        {
            if (c == '/' || c == '\\' || c == ':' || Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0)
            {
                return string.Empty;
            }
        }

        return extension;
    }
}
