// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.DebugTools;

using Windows.Storage;

namespace ComicReader.SDK.Common.Utils;

public static class FileUtils
{
    private const string TAG = nameof(FileUtils);

    public static long GetApproximateDirectorySize(DirectoryInfo directory)
    {
        long size = 0;

        FileInfo[] files;
        try
        {
            files = directory.GetFiles();
        }
        catch (Exception e)
        {
            Logger.E(TAG, "GetApproximateDirectorySize", e);
            files = [];
        }

        foreach (FileInfo file in files)
        {
            try
            {
                size += file.Length;
            }
            catch (Exception e)
            {
                Logger.E(TAG, "GetApproximateDirectorySize", e);
            }
        }

        DirectoryInfo[] dirs;
        try
        {
            dirs = directory.GetDirectories();
        }
        catch (Exception e)
        {
            Logger.E(TAG, "GetDirectorySize", e);
            dirs = [];
        }

        foreach (DirectoryInfo dir in dirs)
        {
            size += GetApproximateDirectorySize(dir);
        }

        return size;
    }

    public static int GetFileHashCode(StorageFile? file)
    {
        if (file == null)
        {
            return 0;
        }

        try
        {
            var fileInfo = new FileInfo(file.Path);
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + fileInfo.LastWriteTime.GetHashCode();
                hash = hash * 23 + fileInfo.Length.GetHashCode();
                return hash;
            }
        }
        catch (FileNotFoundException)
        {
            return 0;
        }
        catch (Exception e)
        {
            Logger.F(TAG, "GetFileHashCode", e);
            return 0;
        }
    }
}
