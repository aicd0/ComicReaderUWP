// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Common.Legacy;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Utils;

using Windows.Storage;

namespace ComicReaderUWP.Data.Models.Comic;

internal partial class ArchiveComicHandle : ComicHandle
{
    private const string TAG = nameof(ArchiveComicHandle);

    public static ComicHandle FromDatabase(string location)
    {
        return new ArchiveComicHandle(location, false);
    }

    public static ComicHandle FromExternal(StorageFile archive)
    {
        var comic = new ArchiveComicHandle(archive.Path, true)
        {
            Title1 = archive.DisplayName,
            _archive = archive,
        };

        return comic;
    }

    public override bool IsEditable => !IsExternal;
    public override string FileExplorerPath => ArchiveAccess.GetBasePath(Location, false);

    private StorageFile? _archive;
    private List<string> _entries = [];

    private ArchiveComicHandle(string location, bool external) : base(ComicType.Archive, external)
    {
        Location = location;
    }

    public override IReadOnlyList<string> GetFolderViewPath()
    {
        string[] pieces = Location.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length == 0)
        {
            return pieces;
        }

        return pieces[..^1];
    }

    protected override async Task<IComicConnection?> OpenComicConnection()
    {
        if (!await ReloadImages())
        {
            return null;
        }

        StorageFile? archive = _archive;
        if (archive is null)
        {
            Logger.AssertNotReachHere("");
            return null;
        }

        List<string> entries = IsExternal ? _entries : [.. _entries.Select(GetSubPathFromFilename)];
        return new ArchiveComicConnection(this, archive, entries);
    }

    private async Task<StorageFile?> GetArchive()
    {
        StorageFile? archive = _archive;
        if (archive != null)
        {
            return archive;
        }

        if (Location == null)
        {
            return null;
        }

        string basePath = ArchiveAccess.GetBasePath(Location, false);
        archive = await Storage.TryGetFile(basePath);
        if (archive == null)
        {
            return null;
        }

        _archive = archive;
        return archive;
    }

    private string GetSubPathFromFilename(string filename)
    {
        Logger.Assert(!IsExternal, "E811B52BC50C1652");
        string subPath = ArchiveAccess.GetSubPath(Location, false);
        if (subPath.Length == 0)
        {
            return filename;
        }
        else
        {
            return subPath + "\\" + filename;
        }
    }

    private async Task<bool> ReloadImages()
    {
        StorageFile? archive = await GetArchive();
        if (archive is null)
        {
            return false;
        }

        // Load entries.
        Logger.I(TAG, $"Retrieving images in '{Location}'...");
        var entries = new List<string>();
        if (IsExternal)
        {
            var ctx = new SearchContext(Location, PathType.File);
            string basePath = ArchiveAccess.GetBasePath(Location, false) + ArchiveAccess.FileSeperator;
            while (await ctx.Search(512))
            {
                foreach (string filepath in ctx.Files)
                {
                    if (filepath.Length <= basePath.Length)
                    {
                        Logger.AssertNotReachHere("46158BE005A1988A");
                        continue;
                    }

                    string filename = StringUtils.ItemNameFromPath(filepath);
                    string extension = StringUtils.ExtensionFromFilename(filename);
                    if (AppInfoProvider.IsSupportedImageExtension(extension))
                    {
                        entries.Add(filepath[basePath.Length..]);
                    }
                }
            }
        }
        else
        {
            string subPath = ArchiveAccess.GetSubPath(Location, false);
            var subfiles = new List<string>();
            await ArchiveAccess.TryGetSubFiles(archive, subPath, subfiles);
            if (subfiles.Count == 0)
            {
                return false;
            }

            foreach (string subfile in subfiles)
            {
                string extension = StringUtils.ExtensionFromFilename(subfile);
                if (!AppInfoProvider.IsSupportedImageExtension(extension))
                {
                    continue;
                }

                entries.Add(subfile);
            }
        }

        _entries = [.. entries.OrderBy(x => StringUtils.SmartFileNameKeySelector(x), StringUtils.SmartFileNameComparer)];
        return true;
    }

    private partial class ArchiveComicConnection(ArchiveComicHandle comic, StorageFile archiveFile, List<string> entries) : IComicConnection
    {
        private readonly StorageFile _archiveFile = archiveFile;
        private readonly List<string> _entries = entries;

        public void Dispose()
        {
        }

        public int GetImageCount()
        {
            return _entries.Count;
        }

        public string GetImageName(int index)
        {
            if (index < 0 || index >= _entries.Count)
            {
                Logger.F(TAG, "GetImageName");
                return string.Empty;
            }

            return _entries[index];
        }

        public string GetImageCacheKey(int index)
        {
            StorageFile? archive = _archiveFile;
            if (archive == null)
            {
                Logger.AssertNotReachHere("");
                return string.Empty;
            }

            if (index >= _entries.Count)
            {
                Logger.AssertNotReachHere("");
                return string.Empty;
            }

            string subPath = comic.IsExternal ? _entries[index] : comic.GetSubPathFromFilename(_entries[index]);
            return archive.Path + ArchiveAccess.FileSeperator + subPath;
        }

        public string GetImageSignature(int index)
        {
            return FileUtils.GetFileSignature(_archiveFile.Path);
        }

        public Stream? OpenImageStream(int index)
        {
            if (index < 0 || index >= _entries.Count)
            {
                Logger.F(TAG, "GetImageStream");
                return null;
            }

            string path = _entries[index];
            Stream? stream = ArchiveAccess.TryGetFileStream(_archiveFile, path).Result;
            if (stream == null)
            {
                Logger.I(TAG, "Failed to access entry '" + _entries[index] + "'");
                return null;
            }

            return stream;
        }

        public IVectorImageService? OpenVectorService(int index)
        {
            return null;
        }
    }
}
