// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Utils;
using ComicReader.Data.Tables;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Database.SqlHelpers;
using ComicReader.SDK.Plugins.Comic;

using Windows.Storage;

namespace ComicReader.Data.Models.Comic;

internal sealed class ComicModel : IComicModel
{
    private readonly ComicData _internalModel;

    private ComicModel(ComicData comicData)
    {
        _internalModel = comicData;
    }

    //
    // Getters
    //

    public string CoverImageCacheKey => _internalModel.GetCoverImageCacheKey();
    public string Description => _internalModel.Description;
    public bool Hidden => _internalModel.Hidden;
    public long Id => _internalModel.Id;
    public bool IsEditable => _internalModel.IsEditable;
    public bool IsExternal => _internalModel.IsExternal;
    public double LastPosition => _internalModel.LastPosition;
    public string Location => _internalModel.Location;
    public int Progress => _internalModel.Progress;
    public DateTimeOffset LastVisit => _internalModel.LastVisit;
    public int Rating => _internalModel.Rating;
    public IReadOnlyList<ComicData.TagData> Tags => _internalModel.Tags;
    public string Title => _internalModel.Title;
    public string Title1 => _internalModel.Title1;
    public string Title2 => _internalModel.Title2;
    public ComicCompletionStatusEnum CompletionState => _internalModel.CompletionState;
    public int PageCount => _internalModel.PageCount;

    public Dictionary<string, HashSet<string>> TagsCopy
    {
        get
        {
            Dictionary<string, HashSet<string>> tagsCopy = [];
            foreach (ComicData.TagData tagData in _internalModel.Tags)
            {
                if (!tagsCopy.TryGetValue(tagData.Name, out HashSet<string>? tagSet))
                {
                    tagSet = [];
                    tagsCopy[tagData.Name] = tagSet;
                }

                foreach (string tag in tagData.Tags)
                {
                    tagSet.Add(tag);
                }
            }

            return tagsCopy;
        }
    }

    public string? GetExt(string key)
    {
        return _internalModel.GetExt(key);
    }

    public string GetImageCacheKey(int index)
    {
        return _internalModel.GetImageCacheKey(index);
    }

    public int GetImageSignature(int index)
    {
        return _internalModel.GetImageSignature(index);
    }

    //
    // Setters
    //

    public void SetExt(string key, string? value)
    {
        _internalModel.SetExt(key, value);
        DispatchUpdateEvent();
    }

    public async Task FlushExt()
    {
        await _internalModel.FlushExt();
    }

    public async Task SetTitle1(string title)
    {
        await _internalModel.SetTitle1(title);
        DispatchUpdateEvent();
    }

    public async Task SetTitle2(string title)
    {
        await _internalModel.SetTitle2(title);
        DispatchUpdateEvent();
    }

    public async Task SetDescription(string description)
    {
        await _internalModel.SetDescription(description);
        DispatchUpdateEvent();
    }

    public async Task SetRating(int rating)
    {
        await _internalModel.SetRating(rating);
        DispatchUpdateEvent();
    }

    public async Task SetTags(IReadOnlyDictionary<string, HashSet<string>> tags)
    {
        await _internalModel.SetTags(tags);
        DispatchUpdateEvent();
    }

    public async Task SetCompletionStateToNotStarted()
    {
        await SetProgress(-1, 0);
        await _internalModel.SaveCompletionState(ComicCompletionStatusEnum.NotStarted);
        DispatchUpdateEvent();
    }

    public async Task SetCompletionStateToStarted()
    {
        _internalModel.SetAsStarted();
        await _internalModel.SaveCompletionState(ComicCompletionStatusEnum.Started);
        DispatchUpdateEvent();
    }

    public async Task SetCompletionStateToAtLeastStarted()
    {
        _internalModel.SetAsStarted();
        if (CompletionState == ComicCompletionStatusEnum.NotStarted)
        {
            await _internalModel.SaveCompletionState(ComicCompletionStatusEnum.Started);
            DispatchUpdateEvent();
        }
    }

    public async Task SetCompletionStateToCompleted()
    {
        await _internalModel.SaveCompletionState(ComicCompletionStatusEnum.Completed);
        DispatchUpdateEvent();
    }

    public async Task SetProgress(int progress, double lastPosition)
    {
        // This method is expected to be called frequently,
        // so we don't dispatch events to save CPU resources
        await _internalModel.SaveProgressAsync(progress, lastPosition);
    }

    public async Task SetHidden(bool hidden)
    {
        await _internalModel.SaveHiddenAsync(hidden);
        DispatchUpdateEvent();
    }

    public async Task SetLocation(string location)
    {
        await _internalModel.SetLocation(location);
    }

    //
    // Utilities
    //

    public Task<IComicConnection?> OpenComicAsync()
    {
        return _internalModel.OpenComicAsync();
    }

    public Task<bool> ReloadImageFiles()
    {
        return _internalModel.ReloadImageFiles();
    }

    public void ShowInFileExplorer(EventRecorder er)
    {
        string fileExplorerPath = _internalModel.FileExplorerPath;
        if (string.IsNullOrEmpty(fileExplorerPath))
        {
            er.SetError("ShowInFileExplorer: FileExplorerPath is null or empty.", fatal: true);
            return;
        }

        if (File.Exists(fileExplorerPath))
        {
            StartProcess(er, "explorer.exe", $"/select,\"{fileExplorerPath}\"");
        }
        else if (Directory.Exists(fileExplorerPath))
        {
            StartProcess(er, "explorer.exe", $"\"{fileExplorerPath}\"");
        }
        else
        {
            er.SetError($"Path does not exist: {fileExplorerPath}");
        }
    }

    //
    // IComicModel Implementation
    //

    long IComicModel.Id => Id;

    string IComicModel.Location => Location;

    int IComicModel.PageCount => PageCount;

    string IComicModel.Title1 => Title1;

    string IComicModel.Title2 => Title2;

    string IComicModel.Description => Description;

    int IComicModel.Rating => Rating;

    IReadOnlyList<IComicTagCategory> IComicModel.Tags => Tags;

    bool IComicModel.IsHidden => Hidden;

    CompletionStatusEnum IComicModel.CompletionStatus
    {
        get
        {
            return CompletionState switch
            {
                ComicCompletionStatusEnum.NotStarted => CompletionStatusEnum.NotStarted,
                ComicCompletionStatusEnum.Started => CompletionStatusEnum.Started,
                ComicCompletionStatusEnum.Completed => CompletionStatusEnum.Completed,
                _ => throw new ArgumentOutOfRangeException(nameof(CompletionState), "Invalid ComicCompletionStatusEnum value."),
            };
        }
    }

    Task IComicModel.SetTitle1(string title)
    {
        return SetTitle1(title);
    }

    Task IComicModel.SetTitle2(string title)
    {
        return SetTitle2(title);
    }

    Task IComicModel.SetDescription(string description)
    {
        return SetDescription(description);
    }

    Task IComicModel.SetRating(int rating)
    {
        return SetRating(rating);
    }

    Task IComicModel.SetTags(IReadOnlyDictionary<string, HashSet<string>> tags)
    {
        return SetTags(tags);
    }

    Task IComicModel.SetHidden(bool isHidden)
    {
        return SetHidden(isHidden);
    }

    async Task IComicModel.SetCompletionStatus(CompletionStatusEnum status)
    {
        ComicCompletionStatusEnum convertedStatus = status switch
        {
            CompletionStatusEnum.NotStarted => ComicCompletionStatusEnum.NotStarted,
            CompletionStatusEnum.Started => ComicCompletionStatusEnum.Started,
            CompletionStatusEnum.Completed => ComicCompletionStatusEnum.Completed,
            _ => throw new ArgumentOutOfRangeException(nameof(status), "Invalid CompletionStatusEnum value."),
        };
        await _internalModel.SaveCompletionState(convertedStatus);
        DispatchUpdateEvent();
    }

    async Task<bool> IComicModel.MoveToLocation(string location)
    {
        string oldLocation = Location;
        bool success = await _internalModel.MoveToLocation(location);
        if (success)
        {
            _locationPool.TryRemove(oldLocation, out _);
            _locationPool.GetOrAdd(location, this);
            DispatchUpdateEvent();
        }

        return success;
    }

    //
    // Creators
    //

    public static async Task<ComicModel?> FromId(long id, string taskName)
    {
        if (TryGetExisting(id, out ComicModel? model))
        {
            return model;
        }

        ComicData? comicData = await ComicData.FromId(id, taskName);
        if (comicData == null)
        {
            return null;
        }

        return ReplaceWithExisting(comicData);
    }

    public static async Task<ComicModel?> FromLocation(string location, string taskName)
    {
        if (TryGetExisting(location, out ComicModel? model))
        {
            return model;
        }

        ComicData? comicData = await ComicData.FromLocation(location, taskName);
        if (comicData == null)
        {
            return null;
        }

        return ReplaceWithExisting(comicData);
    }

    public static async Task<ComicModel?> FromFile(StorageFile file)
    {
        ComicData? comic = null;
        if (AppInfoProvider.IsSupportedDocumentExtension(file.FileType))
        {
            comic = await ComicData.FromLocation(file.Path, "ComicModelFromFileDocument");
            if (comic == null)
            {
                switch (file.FileType.ToLower())
                {
                    case ".pdf":
                        comic = await ComicPdfData.FromExternal(file);
                        break;
                    default:
                        break;
                }
            }
        }
        else if (AppInfoProvider.IsSupportedArchiveExtension(file.FileType))
        {
            comic = await ComicData.FromLocation(file.Path, "ComicModelFromFileArchive");
            comic ??= await ComicArchiveData.FromExternal(file);
        }

        if (comic == null)
        {
            return null;
        }

        return ReplaceWithExisting(comic);
    }

    public static ComicModel? FromImageFiles(string directory, List<StorageFile> imageFiles)
    {
        ComicData? comic = ComicFolderData.FromExternal(directory, imageFiles);
        if (comic is null)
        {
            return null;
        }

        return ReplaceWithExisting(comic);
    }

    public static async Task<List<ComicModel>> BatchFromId(string taskName, IEnumerable<long> ids)
    {
        HashSet<long> idsUnique = [.. ids];
        List<ComicModel> results = [];
        List<long> requestingIds = [];

        foreach (long id in idsUnique)
        {
            if (TryGetExisting(id, out ComicModel? model))
            {
                results.Add(model);
            }
            else
            {
                requestingIds.Add(id);
            }
        }

        if (requestingIds.Count > 0)
        {
            List<ComicData> requestResults = await ComicData.BatchFromId(requestingIds, taskName);
            foreach (ComicData result in requestResults)
            {
                results.Add(ReplaceWithExisting(result));
            }
        }

        return results;
    }

    //
    // Static Utilities
    //

    public static void UpdateAllComics(string reason)
    {
        ComicData.UpdateAllComics(reason);
    }

    public static Task<List<string>> GetAllTagCategories()
    {
        return ComicData.Enqueue<List<string>>("GetAllTagCategories", () =>
        {
            HashSet<string> tags = [];
            var command = SelectCommand.Create(TagCategoryTable.Instance);
            IReaderToken<string> nameToken = command.PutQueryString(TagCategoryTable.ColumnName);
            command.Distinct();
            using SelectCommand.IReader reader = command.Execute();
            while (reader.Read())
            {
                string name = nameToken.GetValue();
                tags.Add(name);
            }
            return [.. tags];
        });
    }

    //
    // Pool
    //

    private static readonly ConcurrentWeakPool<long, ComicModel> _idPool = new();
    private static readonly ConcurrentWeakPool<string, ComicModel> _locationPool = new();

    private static bool TryGetExisting(long id, [MaybeNullWhen(false)] out ComicModel model)
    {
        return _idPool.TryGetValue(id, out model);
    }

    private static bool TryGetExisting(string location, [MaybeNullWhen(false)] out ComicModel model)
    {
        return _locationPool.TryGetValue(location, out model);
    }

    private static ComicModel ReplaceWithExisting(ComicData comicData)
    {
        var model = new ComicModel(comicData);
        if (comicData.Id >= 0)
        {
            return _idPool.GetOrAdd(model.Id, model);
        }

        if (!model.IsExternal)
        {
            // This should never happen, as all comics in the database should have an ID.
            Logger.AssertNotReachHere("C1A98069CD40CC1A");
        }

        return _locationPool.GetOrAdd(model.Location, model);
    }

    //
    // Static Helpers
    //

    private static void StartProcess(EventRecorder er, string fileName, string arguments)
    {
        try
        {
            Process.Start(fileName, arguments);
        }
        catch (Win32Exception ex)
        {
            er.SetError(ex.Message);
        }
        catch (Exception ex)
        {
            er.SetError(ex, fatal: true);
        }
    }

    private static void DispatchUpdateEvent()
    {
        GlobalEvent.Instance.ComicUpdated.Emit(0);
    }
}
