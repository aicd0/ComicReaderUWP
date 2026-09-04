// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Archive;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Core.Common.Utils;

namespace ComicReaderUWP.Data.Models.Comic;

internal partial class ArchiveComicHandle : ComicHandle
{
    private const string TAG = nameof(ArchiveComicHandle);

    public static ComicHandle FromExternal(string path)
    {
        return new ArchiveComicHandle()
        {
            Location = path,
            Title1 = Path.GetFileNameWithoutExtension(path),
        };
    }

    public override bool IsEditable => !IsExternal;
    public override string FileSystemPath => ArchiveManager.GetBasePath(Location, false);

    protected override ComicType Type => ComicType.Archive;

    public override IReadOnlyList<string> GetFolderViewPath()
    {
        string[] pieces = Location.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length == 0)
        {
            return pieces;
        }

        return pieces[..^1];
    }

    protected override async Task<BaseComicConnection?> OpenComicConnection()
    {
        IReadOnlyList<string> entries = await ReloadImages();
        if (entries.Count == 0)
        {
            return null;
        }

        string archivePath = ArchiveManager.GetBasePath(Location, false);
        return new ArchiveComicConnection(archivePath, entries);
    }

    private async Task<IReadOnlyList<string>> ReloadImages()
    {
        var entries = new List<string>();

        if (IsExternal)
        {
            string basePath = ArchiveManager.GetBasePath(Location, false) + ArchiveManager.ARCHIVE_SEP;
            await TaskDispatcher.DefaultThreadPool.Submit(() =>
            {
                foreach (ComicScanner.ItemInfo itemInfo in ComicScanner.Search(Location, ComicScanner.PathType.Archive))
                {
                    if (itemInfo.Type != ComicScanner.ItemType.File)
                    {
                        continue;
                    }

                    if (itemInfo.Path.Length <= basePath.Length)
                    {
                        Logger.F(TAG, $"Full path '{itemInfo.Path}' is shorter than base path '{basePath}'");
                        continue;
                    }

                    string filename = StringUtils.ItemNameFromPath(itemInfo.Path);
                    string extension = StringUtils.ExtensionFromFilename(filename);
                    if (AppInfoProvider.IsSupportedImageExtension(extension))
                    {
                        entries.Add(itemInfo.Path[basePath.Length..]);
                    }
                }
            });
        }
        else
        {
            string archivePath = ArchiveManager.GetBasePath(Location, false);
            string subPath = ArchiveManager.GetSubPath(Location, false);
            IEnumerable<string> subFiles = [];

            await TaskDispatcher.DefaultThreadPool.Submit(() =>
            {
                try
                {
                    subFiles = ArchiveManager.ListFileEntries(archivePath, subPath);
                }
                catch (ArchiveIOException)
                {
                }
            });

            foreach (string subFile in subFiles)
            {
                string extension = StringUtils.ExtensionFromFilename(subFile);
                if (!AppInfoProvider.IsSupportedImageExtension(extension))
                {
                    continue;
                }

                if (subPath.Length == 0)
                {
                    entries.Add(subFile);
                }
                else
                {
                    entries.Add(subPath + "\\" + subFile);
                }
            }
        }

        return [.. entries.OrderBy(x => StringUtils.SmartFileNameKeySelector(x), StringUtils.SmartFileNameComparer)];
    }

    private partial class ArchiveComicConnection(string archivePath, IReadOnlyList<string> entries) : BaseComicConnection
    {
        private readonly string _archivePath = archivePath;
        private readonly IReadOnlyList<string> _entries = entries;

        public override int ImageCount => _entries.Count;

        public override void Dispose()
        {
        }

        public override string GetImageName(int index)
        {
            if (index < 0 || index >= _entries.Count)
            {
                Logger.F(TAG, "GetImageName");
                return string.Empty;
            }

            string path = _entries[index];
            int spliterIndex = path.IndexOf('\\');

            if (spliterIndex < 0)
            {
                return path;
            }
            else
            {
                return path[(spliterIndex + 1)..];
            }
        }

        public override string GetImageCacheKey(int index)
        {
            if (index >= _entries.Count)
            {
                Logger.AssertNotReachHere("");
                return string.Empty;
            }

            string subPath = _entries[index];
            return _archivePath + ArchiveManager.ARCHIVE_SEP + subPath;
        }

        public override string GetImageSignature(int index)
        {
            return FileUtils.GetFileSignature(_archivePath);
        }

        public override async Task<Stream?> OpenImageStream(int index)
        {
            if (index < 0 || index >= _entries.Count)
            {
                Logger.F(TAG, "GetImageStream");
                return null;
            }

            string path = _entries[index];

            try
            {
                return ArchiveManager.OpenEntry(_archivePath, path);
            }
            catch (ArchiveIOException)
            {
                Logger.I(TAG, $"Failed to access entry :{path}");
                return null;
            }
        }
    }
}
