// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Buffers.Binary;

using ComicReaderUWP.Core.Common.DebugTools;

namespace ComicReaderUWP.Core.Common.Utils;

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
        catch (Exception ex)
        {
            Logger.E(TAG, "GetApproximateDirectorySize", ex);
            files = [];
        }

        foreach (FileInfo file in files)
        {
            try
            {
                size += file.Length;
            }
            catch (Exception ex)
            {
                Logger.E(TAG, "GetApproximateDirectorySize", ex);
            }
        }

        DirectoryInfo[] dirs;
        try
        {
            dirs = directory.GetDirectories();
        }
        catch (Exception ex)
        {
            Logger.E(TAG, "GetDirectorySize", ex);
            dirs = [];
        }

        foreach (DirectoryInfo dir in dirs)
        {
            size += GetApproximateDirectorySize(dir);
        }

        return size;
    }

    public static string GetFileSignature(string path)
    {
        long lastWriteTimeTicks;
        long length;
        try
        {
            FileInfo fileInfo = new(path);
            lastWriteTimeTicks = fileInfo.LastWriteTimeUtc.Ticks;
            length = fileInfo.Length;
        }
        catch (FileNotFoundException)
        {
            return string.Empty;
        }
        catch (Exception ex)
        {
            Logger.F(TAG, "GetFileHashCode", ex);
            return string.Empty;
        }

        Span<byte> buffer = stackalloc byte[16];
        BinaryPrimitives.WriteInt64LittleEndian(buffer[..8], lastWriteTimeTicks);
        BinaryPrimitives.WriteInt64LittleEndian(buffer[8..], length);
        byte[] hash = HashUtils.GetXxHash64(buffer);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
