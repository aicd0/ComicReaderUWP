// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Legacy;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Database.SqlHelpers;
using ComicReaderUWP.Data.Models.TagInfo;
using ComicReaderUWP.Data.Tables;

using Windows.Storage;

namespace ComicReaderUWP.Data.Models.Comic;

internal sealed partial class ComicModel : IEquatable<ComicModel>, SDK.Plugins.Comic.IComicModel
{
    private const string TAG = nameof(ComicModel);

    private readonly ComicHandle _internalModel;

    private ComicModel(ComicHandle comicData)
    {
        _objectId = Interlocked.Increment(ref _highestObjectId);
        _internalModel = comicData;
    }

    //
    // Equality
    //

    private static int _highestObjectId = 0;
    private readonly int _objectId;

    public bool Equals(ComicModel? other)
    {
        if (other is null)
        {
            return false;
        }

        return other._objectId == _objectId;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not ComicModel comic)
        {
            return false;
        }

        return Equals(comic);
    }

    public override int GetHashCode()
    {
        return _objectId.GetHashCode();
    }

    public static bool operator ==(ComicModel? left, ComicModel? right)
        => EqualityComparer<ComicModel>.Default.Equals(left, right);

    public static bool operator !=(ComicModel? left, ComicModel? right)
        => !(left == right);

    //
    // Getters
    //

    public string CoverImageCacheKey => _internalModel.GetCoverImageCacheKey().Result;
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
    public IReadOnlyList<ComicHandle.TagData> Tags => _internalModel.Tags;
    public string Title1 => _internalModel.Title1;
    public string Title2 => _internalModel.Title2;
    public ComicCompletionStatusEnum CompletionState => _internalModel.CompletionState;
    public int PageCount => _internalModel.PageCount;
    public IReadOnlyList<string> FolderViewPath => _internalModel.GetFolderViewPath();

    public string Title
    {
        get
        {
            if (Title1.Length == 0)
            {
                if (Title2.Length == 0)
                {
                    return StringResourceProvider.Instance.Untitled;
                }
                else
                {
                    return Title2;
                }
            }
            else if (Title2.Length == 0)
            {
                return Title1;
            }
            else
            {
                return Title1 + " - " + Title2;
            }
        }
    }

    public Dictionary<string, HashSet<string>> TagsCopy
    {
        get
        {
            Dictionary<string, HashSet<string>> tagsCopy = [];
            foreach (ComicHandle.TagData tagData in _internalModel.Tags)
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
    // SDK.Plugins.Comic.IComicModel Implementation
    //

    long SDK.Plugins.Comic.IComicModel.Id => Id;

    string SDK.Plugins.Comic.IComicModel.Location => Location;

    int SDK.Plugins.Comic.IComicModel.PageCount => PageCount;

    string SDK.Plugins.Comic.IComicModel.Title1 => Title1;

    string SDK.Plugins.Comic.IComicModel.Title2 => Title2;

    string SDK.Plugins.Comic.IComicModel.Description => Description;

    int SDK.Plugins.Comic.IComicModel.Rating => Rating;

    IReadOnlyList<SDK.Plugins.Comic.IComicTagCategory> SDK.Plugins.Comic.IComicModel.Tags => Tags;

    IReadOnlyDictionary<string, string> SDK.Plugins.Comic.IComicModel.Links
    {
        get
        {
            Dictionary<string, string> links = [];

            string? linkJson = GetExt(ComicExt.LINKS);
            var linkModel = TagLinkModel.Parse(linkJson);
            if (linkModel is null)
            {
                return links;
            }

            foreach (TagLinkModel.LinkModel item in linkModel.Links)
            {
                links[item.Name] = item.Link;
            }

            return links;
        }
    }

    bool SDK.Plugins.Comic.IComicModel.IsHidden => Hidden;

    SDK.Plugins.Comic.CompletionStatusEnum SDK.Plugins.Comic.IComicModel.CompletionStatus
    {
        get
        {
            return CompletionState switch
            {
                ComicCompletionStatusEnum.NotStarted => SDK.Plugins.Comic.CompletionStatusEnum.NotStarted,
                ComicCompletionStatusEnum.Started => SDK.Plugins.Comic.CompletionStatusEnum.Started,
                ComicCompletionStatusEnum.Completed => SDK.Plugins.Comic.CompletionStatusEnum.Completed,
                _ => throw new ArgumentOutOfRangeException(nameof(CompletionState), "Invalid ComicCompletionStatusEnum value."),
            };
        }
    }

    Task SDK.Plugins.Comic.IComicModel.SetTitle1(string title)
    {
        return SetTitle1(title);
    }

    Task SDK.Plugins.Comic.IComicModel.SetTitle2(string title)
    {
        return SetTitle2(title);
    }

    Task SDK.Plugins.Comic.IComicModel.SetDescription(string description)
    {
        return SetDescription(description);
    }

    Task SDK.Plugins.Comic.IComicModel.SetRating(int rating)
    {
        return SetRating(rating);
    }

    Task SDK.Plugins.Comic.IComicModel.SetTags(IReadOnlyDictionary<string, HashSet<string>> tags)
    {
        return SetTags(tags);
    }

    Task SDK.Plugins.Comic.IComicModel.SetLinks(IReadOnlyDictionary<string, string> links)
    {
        List<TagLinkModel.LinkModel> linkModels = [];
        foreach (KeyValuePair<string, string> item in links)
        {
            linkModels.Add(new()
            {
                Name = item.Key,
                Link = item.Value,
            });
        }

        TagLinkModel linkModel = new()
        {
            Links = linkModels,
        };
        string json = linkModel.Serialize();
        SetExt(ComicExt.LINKS, json);
        return FlushExt();
    }

    Task SDK.Plugins.Comic.IComicModel.SetHidden(bool isHidden)
    {
        return SetHidden(isHidden);
    }

    async Task SDK.Plugins.Comic.IComicModel.SetCompletionStatus(SDK.Plugins.Comic.CompletionStatusEnum status)
    {
        ComicCompletionStatusEnum convertedStatus = status switch
        {
            SDK.Plugins.Comic.CompletionStatusEnum.NotStarted => ComicCompletionStatusEnum.NotStarted,
            SDK.Plugins.Comic.CompletionStatusEnum.Started => ComicCompletionStatusEnum.Started,
            SDK.Plugins.Comic.CompletionStatusEnum.Completed => ComicCompletionStatusEnum.Completed,
            _ => throw new ArgumentOutOfRangeException(nameof(status), "Invalid CompletionStatusEnum value."),
        };
        await _internalModel.SaveCompletionState(convertedStatus);
        DispatchUpdateEvent();
    }

    async Task<SDK.Plugins.Comic.IComicConnection?> SDK.Plugins.Comic.IComicModel.Open()
    {
        IComicConnection? connection = await OpenComicAsync();
        if (connection is null)
        {
            return null;
        }

        return new PluginComicConnection(connection);
    }

    async Task<bool> SDK.Plugins.Comic.IComicModel.MoveToLocation(string location)
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

    private sealed partial class PluginComicConnection(IComicConnection connection) : SDK.Plugins.Comic.IComicConnection
    {
        public int ImageCount => connection.GetImageCount();

        public void Dispose()
        {
            connection.Dispose();
        }

        public string GetImageName(int index)
        {
            return connection.GetImageName(index);
        }

        public string GetImageSignature(int index)
        {
            return connection.GetImageSignature(index);
        }

        public Stream? OpenImageStream(int index)
        {
            return connection.OpenImageStream(index);
        }
    }

    //
    // Creators
    //

    public static async Task<ComicModel?> FromId(long id)
    {
        if (TryGetExisting(id, out ComicModel? model))
        {
            return model;
        }

        ComicHandle? comicData = await ComicHandle.FromId(id);
        if (comicData == null)
        {
            return null;
        }

        return ReplaceWithExisting(comicData);
    }

    public static async Task<ComicModel?> FromLocation(string location)
    {
        if (TryGetExisting(location, out ComicModel? model))
        {
            return model;
        }

        ComicHandle? comicData = await ComicHandle.FromLocation(location);
        if (comicData == null)
        {
            return null;
        }

        return ReplaceWithExisting(comicData);
    }

    public static async Task<ComicModel?> FromFile(StorageFile file)
    {
        ComicHandle? comic = null;
        if (AppInfoProvider.IsSupportedDocumentExtension(file.FileType))
        {
            comic = await ComicHandle.FromLocation(file.Path);
            if (comic == null)
            {
                switch (file.FileType.ToLower())
                {
                    case ".pdf":
                        comic = PdfComicHandle.FromExternal(file);
                        break;
                    default:
                        break;
                }
            }
        }
        else if (AppInfoProvider.IsSupportedArchiveExtension(file.FileType))
        {
            comic = await ComicHandle.FromLocation(file.Path);
            comic ??= ArchiveComicHandle.FromExternal(file);
        }

        if (comic == null)
        {
            return null;
        }

        return ReplaceWithExisting(comic);
    }

    public static ComicModel? FromImageFiles(string directory, List<StorageFile> imageFiles)
    {
        ComicHandle? comic = FolderComicHandle.FromExternal(directory, imageFiles);
        if (comic is null)
        {
            return null;
        }

        return ReplaceWithExisting(comic);
    }

    public static async Task<ComicModel?> FromExternalLocation(string location)
    {
        if (File.Exists(location))
        {
            string extension = Path.GetExtension(location);
            if (!AppInfoProvider.IsSupportedExternalFileExtension(extension))
            {
                Logger.E(TAG, $"Unsupported file extension: {extension}");
                return null;
            }

            StorageFile? file = await Storage.TryGetFile(location);
            if (file is null)
            {
                Logger.E(TAG, $"File not found: {location}");
                return null;
            }

            ComicModel? comic = await FromFile(file);
            if (comic is null)
            {
                Logger.E(TAG, $"Failed to create comic from file: {location}");
                return null;
            }

            return comic;
        }

        if (Directory.Exists(location))
        {
            ComicModel? comic = await FromLocation(location);
            if (comic is not null)
            {
                return comic;
            }

            string[] filePaths;
            try
            {
                filePaths = Directory.GetFiles(location);
            }
            catch (Exception ex)
            {
                Logger.E(TAG, $"Failed to list files in directory: {location}", ex);
                return null;
            }

            List<StorageFile> files = [];
            foreach (string path in filePaths)
            {
                string extension = Path.GetExtension(path);
                if (!AppInfoProvider.IsSupportedImageExtension(extension))
                {
                    continue;
                }

                StorageFile? file = await Storage.TryGetFile(path);
                if (file is null)
                {
                    Logger.E(TAG, $"File not found: {path}");
                    continue;
                }

                files.Add(file);
            }

            if (files.Count == 0)
            {
                Logger.E(TAG, $"No valid image files found in directory: {location}");
                return null;
            }

            comic = FromImageFiles(location, files);
            if (comic is null)
            {
                Logger.E(TAG, $"Failed to create comic from image files in directory: {location}");
                return null;
            }

            return comic;
        }

        Logger.E(TAG, $"Invalid location: {location}");
        return null;
    }

    public static async Task<List<ComicModel>> BatchFromId(IEnumerable<long> ids)
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
            List<ComicHandle> requestResults = await ComicHandle.BatchFromId(requestingIds);
            foreach (ComicHandle result in requestResults)
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
        ComicHandle.UpdateAllComics(reason);
    }

    public static Task<List<string>> GetAllTagCategories()
    {
        return ComicHandle.Enqueue<List<string>>(() =>
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

    private static ComicModel ReplaceWithExisting(ComicHandle comicData)
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
