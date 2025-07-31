// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
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
using Windows.Storage.Search;
using Windows.Storage.Streams;

namespace ComicReader.Data.Models.Comic;

internal partial class ComicFolderData : ComicData
{
    private const string TAG = nameof(ComicFolderData);

    private StorageFolder? _folder;
    private List<StorageFile> _imageFiles = [];

    public override bool IsEditable => !IsExternal;

    private ComicFolderData(string location, bool external) : base(ComicType.Folder, external)
    {
        Location = location;
    }

    public static ComicData FromDatabase(string location)
    {
        return new ComicFolderData(location, false);
    }

    public static ComicData? FromExternal(string directory, List<StorageFile> imageFiles)
    {
        if (imageFiles.Count == 0)
        {
            return null;
        }

        imageFiles = [.. imageFiles.OrderBy(x => StringUtils.SmartFileNameKeySelector(x.DisplayName), StringUtils.SmartFileNameComparer)];
        return new ComicFolderData(directory, true)
        {
            _imageFiles = imageFiles,
        };
    }

    private async Task<StorageFolder?> GetFolder()
    {
        StorageFolder? folder = _folder;
        if (folder != null)
        {
            return folder;
        }

        if (Location == null)
        {
            return null;
        }

        folder = await Storage.TryGetFolder(Location);
        if (folder == null)
        {
            return null;
        }

        _folder = folder;
        return folder;
    }

    protected override async Task<TaskException> ReloadImages()
    {
        if (IsExternal)
        {
            return TaskException.Success;
        }

        StorageFolder? folder = await GetFolder();
        if (folder is null)
        {
            return TaskException.Failure;
        }

        Logger.I(TAG, $"Retrieving images in '{Location}'...");
        var queryOptions = new QueryOptions
        {
            FolderDepth = FolderDepth.Shallow,
            IndexerOption = IndexerOption.DoNotUseIndexer, // The results from UseIndexerWhenAvailable are incomplete
        };

        foreach (string type in AppInfoProvider.SupportedImageExtensions)
        {
            queryOptions.FileTypeFilter.Add(type);
        }

        StorageFileQueryResult query = folder.CreateFileQueryWithOptions(queryOptions);
        IReadOnlyList<StorageFile> imageFiles = await query.GetFilesAsync();

        // Sort by display name
        _imageFiles = [.. imageFiles.OrderBy(x => StringUtils.SmartFileNameKeySelector(x.DisplayName), StringUtils.SmartFileNameComparer)];
        Logger.I(TAG, $"{imageFiles.Count} images added.");
        return TaskException.Success;
    }

    public override string GetImageCacheKey(int index)
    {
        if (index < 0 || index >= _imageFiles.Count)
        {
            Logger.F(TAG, "GetImageCacheKey");
            return string.Empty;
        }

        return _imageFiles[index].Path;
    }

    public override int GetImageSignature(int index)
    {
        if (index < 0 || index >= _imageFiles.Count)
        {
            Logger.F(TAG, "GetImageSignature");
            return 0;
        }

        return FileUtils.GetFileHashCode(_imageFiles[index]);
    }

    public override async Task<IComicConnection?> OpenComicAsync()
    {
        await LoadImageFiles();
        return new FolderComicConnection(_imageFiles);
    }

    private partial class FolderComicConnection(IEnumerable<StorageFile> imageFiles) : IComicConnection
    {
        private readonly IReadOnlyList<StorageFile> _imageFiles = [.. imageFiles];

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
                Logger.F(TAG, "InternalGetImageStream");
                return null;
            }

            StorageFile imageFile = _imageFiles[index];
            try
            {
                return await imageFile.OpenAsync(FileAccessMode.Read);
            }
            catch (FileNotFoundException)
            {
                Logger.I(TAG, $"File not found: {imageFile.Path}");
                return null;
            }
            catch (Exception e)
            {
                Logger.F(TAG, $"Cannot open '{imageFile.Path}'.", e);
                return null;
            }
        }
    }
}
