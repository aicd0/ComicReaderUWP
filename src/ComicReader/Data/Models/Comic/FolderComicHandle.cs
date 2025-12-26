// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ComicReader.Common.Misc;
using ComicReader.Common.Utils;
using ComicReader.Data.Tables;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Threading;
using ComicReader.SDK.Common.Utils;
using ComicReader.SDK.Database.SqlHelpers;

using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Data.Models.Comic;

internal partial class FolderComicHandle : ComicHandle
{
    private const string TAG = nameof(FolderComicHandle);

    public static ComicHandle FromDatabase(string location)
    {
        return new FolderComicHandle(location, false);
    }

    public static ComicHandle? FromExternal(string directory, List<StorageFile> imageFiles)
    {
        if (imageFiles.Count == 0)
        {
            return null;
        }

        return new FolderComicHandle(directory, true)
        {
            _imageFiles = [.. imageFiles
                .OrderBy(x => StringUtils.SmartFileNameKeySelector(x.DisplayName), StringUtils.SmartFileNameComparer)
                .Select(x => x.Path)],
            Title1 = Path.GetFileName(directory),
        };
    }

    public override bool IsEditable => !IsExternal;

    private List<string> _imageFiles = [];

    private FolderComicHandle(string location, bool external) : base(ComicType.Folder, external)
    {
        Location = location;
    }

    protected override Task<bool> MoveToLocationInternal(string newLocation)
    {
        return CoroutineUtils.CreateTaskAsync("MoveToLocationInternal", TaskDispatcher.LongRunningThreadPool, async () =>
        {
            string sourceDir = Location;
            string targetDir = newLocation;
            if (string.IsNullOrWhiteSpace(sourceDir) || string.IsNullOrWhiteSpace(targetDir))
            {
                return false;
            }

            if (!Directory.Exists(sourceDir))
            {
                return false;
            }

            if (Directory.Exists(targetDir) || File.Exists(targetDir))
            {
                return false;
            }

            List<long> affectingComicIds = [];
            await Enqueue("MoveLocation", () =>
            {
                SelectCommand command = SelectCommand.Create(ComicTable.Instance)
                    .AppendCondition(new LikeCondition(ComicTable.ColumnLocation, sourceDir + "%"));
                IReaderToken<long> comicIdToken = command.PutQueryInt64(ComicTable.ColumnId);
                using SelectCommand.IReader reader = command.Execute();
                while (reader.Read())
                {
                    affectingComicIds.Add(comicIdToken.GetValue());
                }

                return true;
            });

            List<ComicModel> affectingComics = ComicModel.BatchFromId("MoveLocation", affectingComicIds).Result;

            string? targetParent = Path.GetDirectoryName(targetDir);
            if (!string.IsNullOrEmpty(targetParent) && !Directory.Exists(targetParent))
            {
                try
                {
                    Directory.CreateDirectory(targetParent);
                }
                catch (Exception e)
                {
                    Logger.E($"Unable to create target parent directory '{targetParent}'.", e);
                    return false;
                }
            }

            try
            {
                Directory.Move(sourceDir, targetDir);
            }
            catch (Exception e)
            {
                Logger.E($"Unable to move directory.", e);
                return false;
            }

            foreach (ComicModel comic in affectingComics)
            {
                string comicFolder = comic.Location;
                if (string.IsNullOrWhiteSpace(comicFolder))
                {
                    continue;
                }

                if (!StringUtils.FolderContain(sourceDir, comic.Location))
                {
                    continue;
                }

                string relativePath = Path.GetRelativePath(sourceDir, comic.Location);
                string newComicPath = relativePath == "." ? targetDir : Path.Combine(targetDir, relativePath);
                await comic.SetLocation(newComicPath);
            }

            return true;
        });
    }

    protected override async Task<IComicConnection?> OpenComicConnection()
    {
        if (!await ReloadImages())
        {
            return null;
        }

        return new FolderComicConnection(_imageFiles);
    }

    private async Task<bool> ReloadImages()
    {
        if (IsExternal)
        {
            return _imageFiles.Count > 0;
        }

        _imageFiles = [.. Directory.GetFiles(Location)
            .Where(file =>
            {
                string extension = Path.GetExtension(file);
                return AppInfoProvider.IsSupportedImageExtension(extension);
            })
            .OrderBy(file => StringUtils.SmartFileNameKeySelector(Path.GetFileNameWithoutExtension(file)), StringUtils.SmartFileNameComparer)];
        return _imageFiles.Count > 0;
    }

    private partial class FolderComicConnection(IEnumerable<string> imageFiles) : IComicConnection
    {
        private readonly IReadOnlyList<string> _imageFiles = [.. imageFiles];

        public void Dispose()
        {
        }

        public int GetImageCount()
        {
            return _imageFiles.Count;
        }

        public async Task<IRandomAccessStream?> GetImageStream(int index)
        {
            if (index < 0 || index >= _imageFiles.Count)
            {
                Logger.F(TAG, "GetImageStream");
                return null;
            }

            string imageFile = _imageFiles[index];
            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(imageFile);
                return await file.OpenAsync(FileAccessMode.Read);
            }
            catch (FileNotFoundException)
            {
                Logger.I(TAG, $"File not found: {imageFile}");
                return null;
            }
            catch (Exception e)
            {
                Logger.F(TAG, $"Cannot open '{imageFile}'.", e);
                return null;
            }
        }

        public string GetImageName(int index)
        {
            if (index < 0 || index >= _imageFiles.Count)
            {
                Logger.F(TAG, "GetImageName");
                return string.Empty;
            }

            string imageFile = _imageFiles[index];
            return Path.GetFileName(imageFile);
        }

        public string GetImageCacheKey(int index)
        {
            if (index < 0 || index >= _imageFiles.Count)
            {
                Logger.F(TAG, "GetImageCacheKey");
                return string.Empty;
            }

            return _imageFiles[index];
        }

        public int GetImageSignature(int index)
        {
            if (index < 0 || index >= _imageFiles.Count)
            {
                Logger.F(TAG, "GetImageSignature");
                return 0;
            }

            return FileUtils.GetFileHashCode(_imageFiles[index]);
        }
    }
}
