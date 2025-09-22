// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Legacy;
using ComicReader.Common.Utils;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Utils;

using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Data.Models.Comic;

internal partial class ComicArchiveData : ComicData
{
    private const string TAG = nameof(ComicArchiveData);

    private StorageFile? _archive;
    private List<string> _entries = [];

    public override bool IsEditable => !IsExternal;
    public override string FileExplorerPath => ArchiveAccess.GetBasePath(Location, false);

    private ComicArchiveData(string location, bool external) : base(ComicType.Archive, external)
    {
        Location = location;
    }

    public static ComicData FromDatabase(string location)
    {
        return new ComicArchiveData(location, false);
    }

    public static async Task<ComicData> FromExternal(StorageFile archive)
    {
        var comic = new ComicArchiveData(archive.Path, true)
        {
            Title1 = archive.DisplayName,
            _archive = archive,
        };

        _ = await comic.ReloadImageFiles();
        return comic;
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

    protected override async Task<bool> ReloadImages()
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

    public override string GetImageCacheKey(int index)
    {
        StorageFile? archive = _archive;
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

        string subPath = IsExternal ? _entries[index] : GetSubPathFromFilename(_entries[index]);
        return archive.Path + ArchiveAccess.FileSeperator + subPath;
    }

    public override int GetImageSignature(int index)
    {
        return FileUtils.GetFileHashCode(_archive);
    }

    protected override async Task<IComicConnection?> OpenComicConnection()
    {
        await LoadImageFiles();

        StorageFile? archive = _archive;
        if (archive == null)
        {
            Logger.AssertNotReachHere("");
            return null;
        }

        List<string> entries = IsExternal ? _entries : [.. _entries.Select(GetSubPathFromFilename)];
        return new ArchiveComicConnection(archive, entries);
    }

    private partial class ArchiveComicConnection(StorageFile archiveFile, List<string> entries) : IComicConnection
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

        public async Task<IRandomAccessStream?> GetImageStream(int index)
        {
            if (index < 0 || index >= _entries.Count)
            {
                Logger.F(TAG, "GetImageStream");
                return null;
            }

            string path = _entries[index];
            Stream? stream = await ArchiveAccess.TryGetFileStream(_archiveFile, path);
            if (stream == null)
            {
                Log("Failed to access entry '" + _entries[index] + "'");
                return null;
            }

            IRandomAccessStream winStream = stream.AsRandomAccessStream();
            return winStream;
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
    }
}
