// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Imaging;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Core.Database.SqlHelpers;
using ComicReaderUWP.Data.Tables;

using Windows.Storage;

namespace ComicReaderUWP.Data.Models.Comic;

internal partial class FolderComicHandle : ComicHandle
{
    private const string TAG = nameof(FolderComicHandle);

    public static ComicHandle? FromExternal(string directory, List<StorageFile> imageFiles)
    {
        if (imageFiles.Count == 0)
        {
            return null;
        }

        return new FolderComicHandle()
        {
            Location = directory,
            Title1 = Path.GetFileName(directory),
            _imageFiles = [.. imageFiles
                .OrderBy(x => StringUtils.SmartFileNameKeySelector(x.DisplayName), StringUtils.SmartFileNameComparer)
                .Select(x => x.Path)],
        };
    }

    private List<string> _imageFiles = [];

    public override bool IsEditable => !IsExternal;

    protected override ComicType Type => ComicType.Folder;

    public override IReadOnlyList<string> GetFolderViewPath()
    {
        string[] pieces = Location.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length == 0)
        {
            return pieces;
        }

        return pieces[..^1];
    }

    protected override Task<bool> MoveToLocationInternal(string newLocation)
    {
        return TaskDispatcher.LongRunningThreadPool.SubmitAsync(async () =>
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
            await Enqueue(() =>
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

            List<ComicModel> affectingComics = await ComicModel.BatchFromId(affectingComicIds);

            string? targetParent = Path.GetDirectoryName(targetDir);
            if (!string.IsNullOrEmpty(targetParent) && !Directory.Exists(targetParent))
            {
                try
                {
                    Directory.CreateDirectory(targetParent);
                }
                catch (Exception ex)
                {
                    Logger.E($"Unable to create target parent directory '{targetParent}'", ex);
                    return false;
                }
            }

            try
            {
                Directory.Move(sourceDir, targetDir);
            }
            catch (Exception ex)
            {
                Logger.E($"Unable to move directory", ex);
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

        IEnumerable<string> files;
        try
        {
            files = Directory.GetFiles(Location);
        }
        catch (Exception ex)
        {
            Logger.E(TAG, $"Cannot access folder '{Location}'", ex);
            return false;
        }

        _imageFiles = [.. files
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

        public int ImageCount => _imageFiles.Count;

        public void Dispose()
        {
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

        public string GetImageSignature(int index)
        {
            if (index < 0 || index >= _imageFiles.Count)
            {
                Logger.F(TAG, "GetImageSignature");
                return string.Empty;
            }

            return FileUtils.GetFileSignature(_imageFiles[index]);
        }

        public async Task<Stream?> OpenImageStream(int index)
        {
            if (index < 0 || index >= _imageFiles.Count)
            {
                Logger.F(TAG, $"OpenImageStream: Index out of range: {index}");
                return null;
            }

            string imageFile = _imageFiles[index];
            try
            {
                return new FileStream(imageFile, FileMode.Open, FileAccess.Read);
            }
            catch (FileNotFoundException ex)
            {
                Logger.E(TAG, $"Cannot open '{imageFile}'", ex);
                return null;
            }
            catch (DirectoryNotFoundException ex)
            {
                Logger.E(TAG, $"Cannot open '{imageFile}'", ex);
                return null;
            }
            catch (IOException ex)
            {
                Logger.E(TAG, $"Cannot open '{imageFile}'", ex);
                return null;
            }
            catch (Exception ex)
            {
                Logger.F(TAG, $"Cannot open '{imageFile}'", ex);
                return null;
            }
        }

        public IVectorImageService? OpenVectorService(int index)
        {
            return null;
        }
    }
}
