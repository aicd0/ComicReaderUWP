// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.SDK.Common.Native;

namespace ComicReaderUWP.Data.Models.Comic;

public enum PathType
{
    Folder,
    File,
}

internal static class SearchContext
{
    public static IEnumerable<ItemInfo> Search(string path, PathType type, int maxDepth = -1)
    {
        List<PathInfo> paths = [new PathInfo(type, path)];
        List<PathInfo> nextPaths = [];
        int depth = 0;
        while (paths.Count > 0)
        {
            foreach (PathInfo pathInfo in paths)
            {
                foreach (ItemInfo item in pathInfo.Ctx.Search())
                {
                    yield return item;

                    if (item.Type == ItemType.File)
                    {
                        string filename = StringUtils.ItemNameFromPath(item.Path);
                        string extension = StringUtils.ExtensionFromFilename(filename);
                        if (AppInfoProvider.IsSupportedArchiveExtension(extension))
                        {
                            nextPaths.Add(new PathInfo(PathType.File, item.Path));
                        }
                    }
                    else if (item.Type == ItemType.Folder)
                    {
                        nextPaths.Add(new PathInfo(PathType.Folder, item.Path));
                    }
                }
            }

            if (maxDepth >= 0 && depth >= maxDepth)
            {
                break;
            }

            depth++;
            (paths, nextPaths) = (nextPaths, paths);
            nextPaths.Clear();
        }
    }

    private class PathInfo(PathType pathType, string path)
    {
        public readonly PathType Type = pathType;
        public readonly string Path = path;

        public readonly IStorageItemSearchContext Ctx = pathType switch
        {
            PathType.Folder => new FolderSearchContext(path),
            PathType.File => new ArchiveSearchContext(path),
            _ => throw new ArgumentException(null, nameof(pathType)),
        };
    }

    public enum ItemType
    {
        Folder,
        File,
        NoAccess,
    }

    public struct ItemInfo
    {
        public ItemType Type;
        public string Path;
    }

    private interface IStorageItemSearchContext
    {
        IEnumerable<ItemInfo> Search();
    }

    private class FolderSearchContext(string path) : IStorageItemSearchContext
    {
        // System Error Codes
        // https://docs.microsoft.com/en-us/windows/win32/debug/system-error-codes--0-499-?redirectedfrom=MSDN
        internal const int ERROR_ACCESS_DENIED = 5;

        internal const int FIND_FIRST_EX_CASE_SENSITIVE = 1;
        internal const int FIND_FIRST_EX_LARGE_FETCH = 2;
        internal const int FIND_FIRST_EX_ON_DISK_ENTRIES_ONLY = 4;

        internal const int GENERIC_READ = unchecked((int)0x80000000);
        internal const int GENERIC_ALL = unchecked(0x10000000);

        internal const int CREATE_NEW = 1;
        internal const int CREATE_ALWAYS = 2;
        internal const int OPEN_EXISTING = 3;
        internal const int OPEN_ALWAYS = 4;
        internal const int TRUNCATE_EXISTING = 5;

        internal const int FILE_ATTRIBUTE_NORMAL = 0x80;

        private readonly string _path = path;

        public IEnumerable<ItemInfo> Search()
        {
            return SubItems(_path, "*");
        }

        private static IEnumerable<ItemInfo> SubItems(string path, string name)
        {
            NativeModels.FindExInfoLevel findInfoLevel;
            NativeModels.FIndexSearchOps indexSearchOps = NativeModels.FIndexSearchOps.FindExSearchNameMatch;
            int additionalFlags;

            if (Environment.OSVersion.Version.Major >= 6)
            {
                findInfoLevel = NativeModels.FindExInfoLevel.FindExInfoBasic;
                additionalFlags = FIND_FIRST_EX_LARGE_FETCH;
            }
            else
            {
                findInfoLevel = NativeModels.FindExInfoLevel.FindExInfoStandard;
                additionalFlags = 0;
            }

            if (!path.EndsWith('\\'))
            {
                path += "\\";
            }

            nint hFile = NativeMethods.FindFirstFileExFromApp(path + name, findInfoLevel,
                out _, indexSearchOps, nint.Zero, additionalFlags);
            if (hFile.ToInt64() == -1)
            {
                yield break;
            }

            while (NativeMethods.FindNextFile(hFile, out NativeModels.Win32FindData find_data))
            {
                string fullpath = path + find_data.cFileName;
                if (((FileAttributes)find_data.dwFileAttributes & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    if (find_data.cFileName == "..")
                    {
                        continue;
                    }

                    yield return new ItemInfo
                    {
                        Type = ItemType.Folder,
                        Path = fullpath,
                    };
                }
                else
                {
                    yield return new ItemInfo
                    {
                        Type = ItemType.File,
                        Path = fullpath,
                    };
                }
            }

            NativeMethods.FindClose(hFile);
        }
    }

    private class ArchiveSearchContext(string path) : IStorageItemSearchContext
    {
        private readonly string _path = path;
        private readonly string _extension = StringUtils.ExtensionFromFilename(path);

        public IEnumerable<ItemInfo> Search()
        {
            using Stream? stream = ArchiveAccess.TryGetFileStream(_path).Result;
            List<string> files = [];
            HashSet<string> folders = [];
            ArchiveAccess.TryReadEntries(stream, _extension, entry =>
            {
                string path = entry.FullName.Replace('/', '\\');
                if (entry.IsDirectory)
                {
                    folders.Add(path[..^1]);
                }
                else
                {
                    files.Add(path);
                    for (int i = 0; (i = path.IndexOf('\\', i)) >= 0; ++i)
                    {
                        folders.Add(path[..i]);
                    }
                }

                return Task.FromResult(ArchiveAccess.ICallbackResult.Continue);
            }).Wait();

            foreach (string file in files)
            {
                yield return new ItemInfo
                {
                    Type = ItemType.File,
                    Path = _path + ArchiveAccess.FileSeperator + file,
                };
            }

            foreach (string folder in folders)
            {
                yield return new ItemInfo
                {
                    Type = ItemType.Folder,
                    Path = _path + ArchiveAccess.FileSeperator + folder,
                };
            }
        }
    }
}
