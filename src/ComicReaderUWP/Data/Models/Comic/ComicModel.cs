// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Archive;
using ComicReaderUWP.Common.ErrorHandling;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Storage;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Core.Database.SqlHelpers;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Data.Models.TagInfo;
using ComicReaderUWP.Data.Tables;
using ComicReaderUWP.SDK.Models;

namespace ComicReaderUWP.Data.Models.Comic;

internal sealed partial class ComicModel : IEquatable<ComicModel>, SDK.Plugins.Comic.IComicModel
{
    private const string TAG = nameof(ComicModel);

    //
    // Static Variables
    //

    private static int _highestObjectId = 0;
    private readonly int _objectId;
    private static readonly MutableLiveData<bool> _isScanningLibraryLiveData = new(false);
    public static LiveData<bool> IsScanningLibraryLiveData => _isScanningLibraryLiveData;

    private static readonly Lock _alterLibraryLock = new();
    private static readonly Queue<QueuedTask> _pendingAlterLibraryTasks = [];
    private static bool _isAlteringLibrary = false;
    private static bool _isLibraryScanPending = false;

    //
    // Static Methods
    //

    public static void RescanLibrary(string reason)
    {
        Logger.I(TAG, $"UpdateAllComics (reason={reason})");

        lock (_alterLibraryLock)
        {
            if (_isLibraryScanPending)
            {
                return;
            }

            _isLibraryScanPending = true;
        }

        AlterLibrary(() => Task.CompletedTask); // Start a worker if not
    }

    public static Task RemoveComics(IEnumerable<ComicModel> comics)
    {
        List<ComicModel> removingComics = [.. comics.Where(x => !x.IsExternal)];

        if (removingComics.Count == 0)
        {
            return Task.CompletedTask;
        }

        HashSet<long> ids = [];
        foreach (ComicModel comic in removingComics)
        {
            long id = comic.Id;
            ids.Add(id);

            comic._internalModel.MarkAsExternal();
            _locationPool.GetOrAdd(comic.Location, comic);
            _idPool.TryRemove(id, out _);
        }

        return AlterLibrary(async () =>
        {
            await RemoveComicsInternal(ids);
            DispatchUpdateEvent();
        });
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

    private static Task AlterLibrary(Func<Task> func)
    {
        QueuedTask item = new(func);
        bool shouldStartWorker;

        lock (_alterLibraryLock)
        {
            _pendingAlterLibraryTasks.Enqueue(item);
            shouldStartWorker = !_isAlteringLibrary;
            _isAlteringLibrary = true;
        }

        if (shouldStartWorker)
        {
            TaskDispatcher.DefaultThreadPool.SubmitAsync(AlterLibraryWorker);
        }

        return item.Task;
    }

    private static async Task AlterLibraryWorker()
    {
        while (true)
        {
            QueuedTask? next;

            lock (_alterLibraryLock)
            {
                if (_pendingAlterLibraryTasks.Count > 0)
                {
                    next = _pendingAlterLibraryTasks.Dequeue();
                }
                else if (_isLibraryScanPending)
                {
                    _isLibraryScanPending = false;
                    next = new(async () =>
                    {
                        _isScanningLibraryLiveData.Emit(true);
                        try
                        {
                            await RescanLibraryInternal();
                        }
                        finally
                        {
                            _isScanningLibraryLiveData.Emit(false);
                        }
                    });
                }
                else
                {
                    _isAlteringLibrary = false;
                    return;
                }
            }

            await next.ExecuteAsync();
        }
    }

    private static async Task RescanLibraryInternal()
    {
        AppSettingsModel.ExternalModel appSettings = AppSettingsModel.GetModel();
        bool comicUpdatedSinceLastBroadcast = false;

        // Get all locations from database
        HashSet<string> oldLocations = [];
        await ComicHandle.Enqueue(() =>
        {
            SelectCommand command = SelectCommand.Create(ComicTable.Instance)
                .AppendCondition(ComicHandle.CreateComicOnlyCondition());
            IReaderToken<string> locationToken = command.PutQueryString(ComicTable.ColumnLocation);
            using SelectCommand.IReader reader = command.Execute();
            while (reader.Read())
            {
                oldLocations.Add(locationToken.GetValue());
            }
        });

        // Scan comics
        Dictionary<string, ComicType> pendingLocations = [];
        HashSet<string> newLocations = [];
        HashSet<string> noAccessLocations = [];

        async Task FlushPendingLocations()
        {
            List<UpdateItemInfo> updateQueue = [];
            foreach (KeyValuePair<string, ComicType> pair in pendingLocations)
            {
                string location = pair.Key;
                ComicType type = pair.Value;

                if (!newLocations.Add(location))
                {
                    continue;
                }

                if (oldLocations.Contains(location))
                {
                    continue;
                }

                if (ComicImportExclusionModel.Instance.Contains(location))
                {
                    continue;
                }

                updateQueue.Add(new UpdateItemInfo
                {
                    Location = location,
                    ItemType = type,
                });
            }

            pendingLocations.Clear();

            if (updateQueue.Count > 0)
            {
                comicUpdatedSinceLastBroadcast = true;
                await TransactionBlock(() =>
                {
                    foreach (UpdateItemInfo info in updateQueue)
                    {
                        ComicHandle.InsertNoLock(info.ItemType, info.Location);
                    }
                });
            }
        }

        var watch = new Stopwatch();
        watch.Start();

        foreach (string folderPath in appSettings.ComicFolders)
        {
            if (!Directory.Exists(folderPath))
            {
                Logger.I(TAG, $"Folder not exists, skipped: {folderPath}");
                continue;
            }

            foreach (ComicScanner.ItemInfo itemInfo in ComicScanner.Search(folderPath, ComicScanner.PathType.Folder))
            {
                if (_isLibraryScanPending)
                {
                    return; // Fast exit
                }

                switch (itemInfo.Type)
                {
                    case ComicScanner.ItemType.Folder:
                        continue;
                    case ComicScanner.ItemType.File:
                        break;
                    case ComicScanner.ItemType.NoAccessLocation:
                        noAccessLocations.Add(itemInfo.Path);
                        continue;
                    default:
                        Logger.F(TAG, $"Unknown item type '{itemInfo.Type}' for path '{itemInfo.Path}'");
                        continue;
                }

                string filename = StringUtils.ItemNameFromPath(itemInfo.Path);
                string extension = StringUtils.ExtensionFromFilename(filename).ToLowerInvariant();
                if (AppInfoProvider.IsSupportedImageExtension(extension))
                {
                    string location = StringUtils.ParentLocationFromLocation(itemInfo.Path);
                    ComicType type = ArchiveManager.IsArchivePath(itemInfo.Path) ? ComicType.Archive : ComicType.Folder;
                    pendingLocations[location] = type;
                }
                else
                {
                    switch (extension)
                    {
                        case ".pdf":
                            pendingLocations[itemInfo.Path] = ComicType.PDF;
                            break;
                        default:
                            break;
                    }
                }

                if (watch.LapSpan().TotalSeconds > 2)
                {
                    await FlushPendingLocations();

                    if (comicUpdatedSinceLastBroadcast)
                    {
                        comicUpdatedSinceLastBroadcast = false;
                        DispatchUpdateEvent();
                    }

                    watch.Lap();
                }
            }
        }

        await FlushPendingLocations();

        // Remove unreachable comics
        if (appSettings.RemoveUnreachableComics)
        {
            List<string> locationRemoved = [.. oldLocations.Except(newLocations)];

            for (int i = locationRemoved.Count - 1; i >= 0; i--)
            {
                string location = locationRemoved[i];
                foreach (string noAccessLocation in noAccessLocations)
                {
                    if (StringUtils.FolderContain(noAccessLocation, location))
                    {
                        locationRemoved.RemoveAt(i);
                        break;
                    }
                }
            }

            if (locationRemoved.Count > 0)
            {
                bool proceed = true;
                if (appSettings.PromptBeforeRemovingComics)
                {
                    string promptContent = StringResourceProvider.Instance.ComicRemovalPromptContent
                        .Replace("$count", locationRemoved.Count.ToString())
                        .Replace("$comics", string.Join('\n', locationRemoved));
                    DialogOptions options = new DialogOptions.Builder()
                        .SetTitle(StringResourceProvider.Instance.Warning)
                        .SetContent(promptContent)
                        .SetPrimaryButtonText(StringResourceProvider.Instance.Remove)
                        .SetCloseButtonText(StringResourceProvider.Instance.Cancel)
                        .Build();
                    DialogResult result = await DialogUtils.EnqueueDialogAsync(options);
                    proceed = result == DialogResult.Primary;
                }

                if (proceed)
                {
                    comicUpdatedSinceLastBroadcast = true;

                    List<long> removedComicIds = [];
                    await ComicHandle.Enqueue(() =>
                    {
                        foreach (string location in locationRemoved)
                        {
                            List<long> comicIds = [];
                            SelectCommand command = SelectCommand.Create(ComicTable.Instance)
                                .AppendCondition(ComicTable.ColumnLocation, location)
                                .AppendCondition(ComicHandle.CreateComicOnlyCondition());
                            IReaderToken<long> idToken = command.PutQueryInt64(ComicTable.ColumnId);
                            using SelectCommand.IReader reader = command.Execute();
                            while (reader.Read())
                            {
                                comicIds.Add(idToken.GetValue());
                            }

                            if (comicIds.Count == 0)
                            {
                                Logger.F(TAG, $"Removing: No rows found for location '{location}'");
                                continue;
                            }

                            if (comicIds.Count != 1)
                            {
                                Logger.F(TAG, $"Removing: Found {comicIds.Count} rows for location '{location}'");
                            }

                            Logger.I(TAG, $"Removing: {location}");
                            removedComicIds.AddRange(comicIds);
                        }
                    });

                    await RemoveComicsInternal(removedComicIds);
                }
            }
        }

        if (comicUpdatedSinceLastBroadcast)
        {
            DispatchUpdateEvent();
        }
    }

    private static async Task RemoveComicsInternal(IEnumerable<long> comicIds)
    {
        List<long> ids = [.. comicIds];
        if (ids.Count == 0)
        {
            return;
        }

        List<string> resourceIds = [];
        List<ComicModel> comics = await BatchFromId(ids);
        foreach (ComicModel comic in comics)
        {
            string? resourceId = comic.GetExt(ComicExt.RESOURCE_UUID);
            if (!string.IsNullOrEmpty(resourceId))
            {
                resourceIds.Add(resourceId);
            }

            _idPool.TryRemove(comic.Id, out _);
        }

        await ComicHandle.Enqueue(() =>
        {
            SqliteDB.MainDatabase.WithTransaction(() =>
            {
                foreach (IEnumerable<long> idChunk in SqlUtils.ChunkBy(ids))
                {
                    DeleteCommand.Create(TagTable.Instance)
                        .AppendCondition(new InCondition(ColumnOrValue.FromColumn(TagTable.ColumnComicId), idChunk))
                        .Execute();
                    DeleteCommand.Create(TagCategoryTable.Instance)
                        .AppendCondition(new InCondition(ColumnOrValue.FromColumn(TagCategoryTable.ColumnComicId), idChunk))
                        .Execute();
                    DeleteCommand.Create(ComicCollectionTable.Instance)
                        .AppendCondition(new InCondition(ColumnOrValue.FromColumn(ComicCollectionTable.ColumnComicId), idChunk))
                        .Execute();
                    DeleteCommand.Create(ComicCollectionTable.Instance)
                        .AppendCondition(new InCondition(ColumnOrValue.FromColumn(ComicCollectionTable.ColumnCollectionId), idChunk))
                        .Execute();
                    DeleteCommand.Create(ComicTable.Instance)
                        .AppendCondition(new InCondition(ColumnOrValue.FromColumn(ComicTable.ColumnId), idChunk))
                        .Execute();
                }
            });
        });

        await SqliteDB.MiscDatabaseDispatcher.Submit(() =>
        {
            foreach (IEnumerable<long> idChunk in SqlUtils.ChunkBy(ids))
            {
                DeleteCommand.Create(ComicHistoryTable.Instance)
                    .AppendCondition(new InCondition(ColumnOrValue.FromColumn(ComicHistoryTable.ColumnComicId), idChunk))
                    .Execute();
            }
        });

        FavoriteModel.Instance.BatchRemoveWithId([.. ids]);

        List<Task> resourceTasks = [];
        foreach (string resourceId in resourceIds)
        {
            resourceTasks.Add(ResourceManager.Release(resourceId));
        }

        await Task.WhenAll(resourceTasks);
    }

    private static Task TransactionBlock(Action action)
    {
        return ComicHandle.Enqueue(() =>
        {
            SqliteDB.MainDatabase.WithTransaction(action);
        });
    }

    private static void DispatchUpdateEvent()
    {
        GlobalEvent.Instance.ComicUpdated.Emit(0);
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

    private static ComicModel ReplaceWithExisting(ComicHandle comicHandle)
    {
        var model = new ComicModel(comicHandle);

        if (!model.IsExternal)
        {
            return _idPool.GetOrAdd(model.Id, model);
        }

        return _locationPool.GetOrAdd(model.Location, model);
    }

    //
    // Constructors
    //

    private readonly ComicHandle _internalModel;

    private ComicModel(ComicHandle comicData)
    {
        _objectId = Interlocked.Increment(ref _highestObjectId);
        _internalModel = comicData;
    }

    //
    // Equality
    //

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
    // Getters/Setters
    //

    public string Description => _internalModel.Description;
    public bool Hidden => _internalModel.Hidden;
    public long Id => _internalModel.Id;
    public bool IsEditable => _internalModel.IsEditable;
    public bool IsExternal => _internalModel.IsExternal;
    public bool IsCollection => _internalModel.IsCollection;
    public double LastPosition => _internalModel.LastPosition;
    public string Location => _internalModel.Location;
    public int Progress => _internalModel.Progress;
    public DateTimeOffset LastVisit => _internalModel.LastVisit;
    public int Rating => _internalModel.Rating;
    public IReadOnlyDictionary<string, ComicTagCategory> Tags => _internalModel.Tags;
    public string Title1 => _internalModel.Title1;
    public string Title2 => _internalModel.Title2;
    public CompletionStatusEnum CompletionStatus => _internalModel.CompletionStatus;
    public int PageCount => _internalModel.PageCount;
    public IReadOnlyList<string> FolderViewPath => _internalModel.GetFolderViewPath();
    public Dictionary<string, HashSet<string>> TagsCopy => _internalModel.Tags.ToDictionary(p => p.Key, p => p.Value.Tags.ToHashSet());

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

    public string? GetExt(string key)
    {
        return _internalModel.GetExt(key);
    }

    public async Task Save()
    {
        bool wasExternal = IsExternal;
        await _internalModel.Save();

        if (wasExternal)
        {
            _idPool.Set(Id, this);
        }

        DispatchUpdateEvent();
    }

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

    public async Task SetTags<T>(IEnumerable<KeyValuePair<string, T>> tags) where T : IEnumerable<string>
    {
        await _internalModel.SetTags(tags);
        DispatchUpdateEvent();
    }

    public async Task SetCompletionStatus(CompletionStatusEnum status)
    {
        switch (status)
        {
            case CompletionStatusEnum.Unread:
                await SetProgress(-1, 0);
                break;
            case CompletionStatusEnum.Reading:
                _internalModel.SetAsVisited();
                break;
            default:
                break;
        }

        await _internalModel.SaveCompletionStatus(status);
        DispatchUpdateEvent();
    }

    public async Task SetAsVisited()
    {
        _internalModel.SetAsVisited();

        if (CompletionStatusService.CanTransitToReadingAutomatically(CompletionStatus))
        {
            await _internalModel.SaveCompletionStatus(CompletionStatusEnum.Reading);
            DispatchUpdateEvent();
        }
    }

    public async Task SetProgress(int progress, double lastPosition)
    {
        // This method is expected to be called frequently,
        // so we don't dispatch events to save CPU resources
        await _internalModel.SetProgress(progress, lastPosition);
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

    public Task<ErrorResult<ComicConnection>> OpenComic()
    {
        return _internalModel.OpenComic();
    }

    public Task<ErrorResult> ShowInFileExplorer()
    {
        if (IsCollection)
        {
            var err = ErrorLogger.Create(TAG);
            return Task.FromResult(err.Error("A collection has no file location."));
        }

        return ThirdPartyLauncher.ShowInFileExplorer(_internalModel.FileSystemPath);
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

    IReadOnlyDictionary<string, SDK.Plugins.Comic.IComicTagCategory> SDK.Plugins.Comic.IComicModel.Tags => _internalModel.TagsForPlugin;

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

    SDK.Plugins.Comic.CompletionStatusEnum SDK.Plugins.Comic.IComicModel.CompletionStatus =>
        CompletionStatusService.HostEnumToSDKEnum(CompletionStatus);

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

    Task SDK.Plugins.Comic.IComicModel.SetTags<T>(IEnumerable<KeyValuePair<string, T>> tags)
    {
        return SetTags(tags);
    }

    Task SDK.Plugins.Comic.IComicModel.SetLinks(IEnumerable<KeyValuePair<string, string>> links)
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
        CompletionStatusEnum convertedStatus = CompletionStatusService.SDKEnumToHostEnum(status);
        await _internalModel.SaveCompletionStatus(convertedStatus);
        DispatchUpdateEvent();
    }

    async Task<SDK.Plugins.Comic.IComicConnection?> SDK.Plugins.Comic.IComicModel.Open()
    {
        ErrorResult<ComicConnection> connectionErr = await OpenComic();
        if (!connectionErr.IsSuccessful)
        {
            return null;
        }

        return new PluginComicConnection(connectionErr.Result);
    }

    async Task<bool> SDK.Plugins.Comic.IComicModel.MoveToLocation(string location)
    {
        string oldLocation = Location;
        bool success = await _internalModel.MoveToLocation(location);

        if (success)
        {
            if (IsExternal)
            {
                _locationPool.TryRemove(oldLocation, out _);
                _locationPool.GetOrAdd(location, this);
            }

            DispatchUpdateEvent();
        }

        return success;
    }

    private sealed partial class PluginComicConnection(IComicConnection connection) : SDK.Plugins.Comic.IComicConnection
    {
        public int ImageCount => connection.ImageCount;

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

        public Task<Stream?> OpenImageStream(int index)
        {
            return connection.OpenImageStream(index);
        }
    }

    //
    // Creators
    //

    public static ComicModel CreateCollection()
    {
        return new ComicModel(CollectionComicHandle.FromExternal());
    }

    public static async Task<ComicModel?> FromId(long id)
    {
        if (TryGetExisting(id, out ComicModel? model))
        {
            return model;
        }

        ComicHandle? comicHandle = await ComicHandle.FromId(id);
        if (comicHandle is not null)
        {
            return ReplaceWithExisting(comicHandle);
        }

        return null;
    }

    public static async Task<ComicModel?> FromLocation(string location)
    {
        ComicHandle? comicHandle = await ComicHandle.FromLocation(location);
        if (comicHandle is not null)
        {
            return ReplaceWithExisting(comicHandle);
        }

        if (TryGetExisting(location, out ComicModel? model))
        {
            return model;
        }

        return null;
    }

    public static async Task<ComicModel?> FromFile(string path)
    {
        ComicHandle? comic = null;
        string extension = Path.GetExtension(path).ToLower();
        if (AppInfoProvider.IsSupportedDocumentExtension(extension))
        {
            comic = await ComicHandle.FromLocation(path);
            if (comic is null)
            {
                switch (extension)
                {
                    case ".pdf":
                        comic = PdfComicHandle.FromExternal(path);
                        break;
                    default:
                        break;
                }
            }
        }
        else if (AppInfoProvider.IsSupportedArchiveExtension(extension))
        {
            comic = await ComicHandle.FromLocation(path);
            comic ??= ArchiveComicHandle.FromExternal(path);
        }

        if (comic is null)
        {
            return null;
        }

        return ReplaceWithExisting(comic);
    }

    public static ComicModel FromFolder(string directory)
    {
        ComicHandle comic = FolderComicHandle.FromExternal(directory);
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

            ComicModel? comic = await FromFile(location);
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

            List<string> files = [];
            foreach (string path in filePaths)
            {
                string extension = Path.GetExtension(path);
                if (!AppInfoProvider.IsSupportedImageExtension(extension))
                {
                    continue;
                }

                files.Add(path);
            }

            if (files.Count == 0)
            {
                Logger.E(TAG, $"No valid image files found in directory: {location}");
                return null;
            }

            comic = FromFolder(location);
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
        HashSet<long> idsUnique = [.. ids.Where(x => x >= 0)];
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
    // Types
    //

    private struct UpdateItemInfo
    {
        public string Location;
        public ComicType ItemType;
    };

    private sealed class QueuedTask(Func<Task> func)
    {
        private readonly Func<Task> _func = func;
        private readonly TaskCompletionSource _source = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Task => _source.Task;

        public async Task ExecuteAsync()
        {
            try
            {
                await _func();
                _source.SetResult();
            }
            catch (Exception ex)
            {
                _source.SetException(ex);
            }
        }
    }
}
