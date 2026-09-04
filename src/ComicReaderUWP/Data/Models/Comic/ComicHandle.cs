// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Archive;
using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Threading;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Core.Database.SqlHelpers;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.Data.Tables;
using ComicReaderUWP.SDK.Models;

namespace ComicReaderUWP.Data.Models.Comic;

internal abstract partial class ComicHandle
{
    //
    // Constants
    //

    private const string TAG = nameof(ComicHandle);

    //
    // Static Variables
    //

    private static readonly MutableLiveData<bool> _isScanningLibraryLiveData = new(false);
    public static LiveData<bool> IsScanningLibraryLiveData => _isScanningLibraryLiveData;

    private static readonly Lock _alterLibraryLock = new();
    private static readonly Queue<QueuedTask> _pendingAlterLibraryTasks = [];
    private static bool _isAlteringLibrary = false;
    private static bool _isLibraryScanPending = false;

    //
    // Static Methods
    //

    public static Task Enqueue(Action action)
    {
        return SqliteDB.MainDatabaseDispatcher.Submit(action);
    }

    public static Task<T> Enqueue<T>(Func<T> func)
    {
        return SqliteDB.MainDatabaseDispatcher.Submit(func);
    }

    public static Task<ComicHandle?> FromId(long id)
    {
        return Enqueue(() =>
        {
            return FromIdNoLock(id);
        });
    }

    public static Task<ComicHandle?> FromLocation(string location)
    {
        return Enqueue(() =>
        {
            return FromLocationNoLock(location);
        });
    }

    public static Task<List<ComicHandle>> BatchFromId(IEnumerable<long> ids)
    {
        return Enqueue(() =>
        {
            return BatchFromIdNoLock(ids);
        });
    }

    public static Task AlterLibrary(Func<Task> func)
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

    private static Task TransactionBlock(Action action)
    {
        return Enqueue(() =>
        {
            SqliteDB.MainDatabase.WithTransaction(action);
        });
    }

    private static ComicHandle? FromIdNoLock(long id)
    {
        List<ComicHandle> result = BatchFromIdNoLock([id]);
        if (result.Count == 0)
        {
            return null;
        }

        return result[0];
    }

    private static List<ComicHandle> BatchFromIdNoLock(IEnumerable<long> ids)
    {
        {
            bool isEmpty = true;
            foreach (long _ in ids)
            {
                isEmpty = false;
                break;
            }
            if (isEmpty)
            {
                return [];
            }
        }

        Dictionary<long, ComicHandle> comics = new(ids.Count());
        foreach (IEnumerable<long> idChunk in SqlUtils.ChunkBy(ids))
        {
            SelectCommand command = SelectCommand.Create(ComicTable.Instance)
                .AppendCondition(new InCondition(ColumnOrValue.FromColumn(ComicTable.ColumnId), idChunk));
            IReaderToken<long> idToken = command.PutQueryInt64(ComicTable.ColumnId);
            IReaderToken<long> typeToken = command.PutQueryInt64(ComicTable.ColumnType);
            IReaderToken<string> locationToken = command.PutQueryString(ComicTable.ColumnLocation);
            IReaderToken<string> title1Token = command.PutQueryString(ComicTable.ColumnTitle1);
            IReaderToken<string> title2Token = command.PutQueryString(ComicTable.ColumnTitle2);
            IReaderToken<bool> hiddenToken = command.PutQueryBoolean(ComicTable.ColumnHidden);
            IReaderToken<int> ratingToken = command.PutQueryInt32(ComicTable.ColumnRating);
            IReaderToken<int> progressToken = command.PutQueryInt32(ComicTable.ColumnProgress);
            IReaderToken<DateTimeOffset> lastVisitToken = command.PutQueryDateTimeOffset(ComicTable.ColumnLastVisit);
            IReaderToken<double> lastPositionToken = command.PutQueryDouble(ComicTable.ColumnLastPosition);
            IReaderToken<string> descriptionToken = command.PutQueryString(ComicTable.ColumnDescription);
            IReaderToken<int> completionStatusToken = command.PutQueryInt32(ComicTable.ColumnCompletionStatus);
            IReaderToken<int> pageCountToken = command.PutQueryInt32(ComicTable.ColumnPageCount);
            IReaderToken<string> extToken = command.PutQueryString(ComicTable.ColumnExt);
            using SelectCommand.IReader reader = command.Execute();

            while (reader.Read())
            {
                long id = idToken.GetValue();
                var type = (ComicType)typeToken.GetValue();
                string location = locationToken.GetValue();
                string title1 = title1Token.GetValue();
                string title2 = title2Token.GetValue();
                bool hidden = hiddenToken.GetValue();
                int rating = ratingToken.GetValue();
                int progress = progressToken.GetValue();
                DateTimeOffset lastVisit = lastVisitToken.GetValue();
                double lastPosition = lastPositionToken.GetValue();
                string description = descriptionToken.GetValue();
                CompletionStatusEnum completionStatus = ParseCompletionStatus(completionStatusToken.GetValue());
                int pageCount = pageCountToken.GetValue();
                string extJson = extToken.GetValue();

                ComicHandle? comic = FromType(type);
                if (comic is null)
                {
                    continue;
                }

                comic.Id = id;
                comic.Location = location;
                comic.Title1 = title1;
                comic.Title2 = title2;
                comic.Hidden = hidden;
                comic.Rating = rating;
                comic.Progress = progress;
                comic.LastVisit = lastVisit;
                comic.LastPosition = lastPosition;
                comic.Description = description;
                comic._tags = new([]);
                comic.CompletionStatus = completionStatus;
                comic.PageCount = pageCount;

                if (!string.IsNullOrEmpty(extJson))
                {
                    Dictionary<string, string>? ext = null;

                    try
                    {
                        ext = JsonSerializer.Deserialize<Dictionary<string, string>>(extJson);
                    }
                    catch (Exception ex)
                    {
                        Logger.F(TAG, ex);
                    }

                    if (ext != null)
                    {
                        foreach (KeyValuePair<string, string> pair in ext)
                        {
                            comic._ext[pair.Key] = pair.Value;
                        }
                    }
                }

                comics[id] = comic;
            }
        }

        Dictionary<long, TagTempData> tagCategories = new(comics.Count);
        foreach (IEnumerable<long> idChunk in SqlUtils.ChunkBy(comics.Keys))
        {
            SelectCommand command = SelectCommand.Create(TagCategoryTable.Instance)
                .AppendCondition(new InCondition(ColumnOrValue.FromColumn(TagCategoryTable.ColumnComicId), idChunk));
            IReaderToken<long> comicIdToken = command.PutQueryInt64(TagCategoryTable.ColumnComicId);
            IReaderToken<long> tagCategoryIdToken = command.PutQueryInt64(TagCategoryTable.ColumnId);
            IReaderToken<string> nameToken = command.PutQueryString(TagCategoryTable.ColumnName);
            using SelectCommand.IReader reader = command.Execute();

            while (reader.Read())
            {
                long comicId = comicIdToken.GetValue();
                if (comics.ContainsKey(comicId))
                {
                    long tagCategoryId = tagCategoryIdToken.GetValue();
                    string name = nameToken.GetValue();
                    var tagData = new TagTempData
                    {
                        Name = name,
                        ComicId = comicId,
                    };
                    tagCategories[tagCategoryId] = tagData;
                }
            }
        }

        foreach (IEnumerable<long> idChunk in SqlUtils.ChunkBy(tagCategories.Keys))
        {
            SelectCommand command = SelectCommand.Create(TagTable.Instance)
                .AppendCondition(new InCondition(ColumnOrValue.FromColumn(TagTable.ColumnTagCategoryId), idChunk));
            IReaderToken<long> tagCategoryIdToken = command.PutQueryInt64(TagTable.ColumnTagCategoryId);
            IReaderToken<string> tagToken = command.PutQueryString(TagTable.ColumnContent);
            using SelectCommand.IReader reader = command.Execute();

            while (reader.Read())
            {
                long tagCategoryId = tagCategoryIdToken.GetValue();
                if (tagCategories.TryGetValue(tagCategoryId, out TagTempData? tagData))
                {
                    string tag = tagToken.GetValue();
                    tagData.Tags.Add(tag);
                }
            }
        }

        {
            Dictionary<long, Dictionary<string, ComicTagCategory>> comicTags = [];
            foreach (TagTempData tagCategory in tagCategories.Values)
            {
                if (comics.TryGetValue(tagCategory.ComicId, out _))
                {
                    if (!comicTags.TryGetValue(tagCategory.ComicId, out Dictionary<string, ComicTagCategory>? tags))
                    {
                        tags = [];
                        comicTags[tagCategory.ComicId] = tags;
                    }

                    tags[tagCategory.Name] = new(tagCategory.Tags);
                }
            }

            foreach (KeyValuePair<long, Dictionary<string, ComicTagCategory>> pair in comicTags)
            {
                if (comics.TryGetValue(pair.Key, out ComicHandle? comic))
                {
                    comic._tags = new(pair.Value);
                }
            }
        }

        return [.. comics.Values];
    }

    private static ComicHandle? FromLocationNoLock(string location)
    {
        SelectCommand command = SelectCommand.Create(ComicTable.Instance)
            .AppendCondition(ComicTable.ColumnLocation, location)
            .Limit(1);
        IReaderToken<long> comicIdToken = command.PutQueryInt64(ComicTable.ColumnId);
        using SelectCommand.IReader reader = command.Execute();

        if (!reader.Read())
        {
            return null;
        }

        long comicId = comicIdToken.GetValue();
        return FromIdNoLock(comicId);
    }

    private static ComicHandle? FromType(ComicType type)
    {
        switch (type)
        {
            case ComicType.Folder:
                return new FolderComicHandle();
            case ComicType.Archive:
                return new ArchiveComicHandle();
            case ComicType.PDF:
                return new PdfComicHandle();
            default:
                Logger.F(TAG, $"Unknown comic type: {type}");
                return null;
        }
    }

    private static CompletionStatusEnum ParseCompletionStatus(int value)
    {
        if (Enum.IsDefined(typeof(CompletionStatusEnum), value))
        {
            return (CompletionStatusEnum)value;
        }

        return CompletionStatusEnum.Unread;
    }

    private static void RemoveWithLocationNoLock(string location)
    {
        int count = DeleteCommand.Create(ComicTable.Instance)
            .AppendCondition(ComicTable.ColumnLocation, location)
            .Execute();
        if (count != 1)
        {
            Logger.F(TAG, $"RemoveWithLocationNoLock: Deleted {count} rows for location '{location}'");
        }
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
        AppSettingsModel.ExternalModel appSettings = AppSettingsModel.Instance.GetModel();
        bool comicUpdatedSinceLastBroadcast = false;

        // Get all locations from database
        HashSet<string> oldLocations = [];
        await Enqueue(() =>
        {
            var command = SelectCommand.Create(ComicTable.Instance);
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
                newLocations.Add(location);

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
                        ComicHandle? comic = FromType(info.ItemType);
                        if (comic is null)
                        {
                            continue;
                        }

                        comic.Location = info.Location;
                        comic.SetAsDefaultInfo();

                        var command = InsertCommand.Create(ComicTable.Instance);
                        foreach (IColumnTypeless column in _allNonIdColumns.Value)
                        {
                            command.AppendColumn(column, comic.GetColumnValue(column));
                        }

                        comic.Id = command.Execute();
                        comic.InternalSaveTagsNoLock();
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
                        DispatchComicUpdateEvent();
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
                    await TransactionBlock(() =>
                    {
                        foreach (string location in locationRemoved)
                        {
                            Logger.I(TAG, $"Removing: {location}");
                            RemoveWithLocationNoLock(location);
                        }
                    });
                }
            }
        }

        if (comicUpdatedSinceLastBroadcast)
        {
            DispatchComicUpdateEvent();
        }
    }

    private static void DispatchComicUpdateEvent()
    {
        GlobalEvent.Instance.ComicUpdated.Emit(0);
    }

    //
    // Member variables
    //

    private ReadOnlyTags _tags = new([]);
    private readonly ConcurrentDictionary<string, string> _ext = [];

    //
    // Properties
    //

    public long Id { get; private set; } = -1;
    public CompletionStatusEnum CompletionStatus { get; private set; }
    public string Location { get; protected set; } = string.Empty;
    public string Title1 { get; protected set; } = string.Empty;
    public string Title2 { get; protected set; } = string.Empty;
    public bool Hidden { get; protected set; } = false;
    public int Rating { get; protected set; } = -1;
    public int Progress { get; protected set; } = -1;
    public DateTimeOffset LastVisit { get; protected set; } = DateTimeOffset.MinValue;
    public double LastPosition { get; protected set; } = 0.0;
    public string Description { get; private set; } = string.Empty;
    public IReadOnlyDictionary<string, ComicTagCategory> Tags => _tags;
    public IReadOnlyDictionary<string, SDK.Plugins.Comic.IComicTagCategory> TagsForPlugin => _tags;
    public int PageCount { get; private set; } = -1;

    public abstract bool IsEditable { get; }
    public virtual string FileSystemPath => Location;
    public bool IsExternal => Id < 0;

    protected abstract ComicType Type { get; }

    //
    // Getters
    //

    public string? GetExt(string key)
    {
        if (_ext.TryGetValue(key, out string? value))
        {
            return value;
        }
        return null;
    }

    //
    // Setters
    //

    public void SetExt(string key, string? value)
    {
        if (value is null)
        {
            _ext.Remove(key, out _);
        }
        else
        {
            _ext[key] = value;
        }
    }

    public async Task FlushExt()
    {
        await Enqueue(() =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnExt, GetColumnValue(ComicTable.ColumnExt))
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        });
    }

    public async Task SetTitle1(string title)
    {
        Title1 = title;
        await Enqueue(() =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnTitle1, GetColumnValue(ComicTable.ColumnTitle1))
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        });
    }

    public async Task SetTitle2(string title)
    {
        Title2 = title;
        await Enqueue(() =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnTitle2, GetColumnValue(ComicTable.ColumnTitle2))
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        });
    }

    public async Task SetDescription(string description)
    {
        Description = description;
        await Enqueue(() =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnDescription, GetColumnValue(ComicTable.ColumnDescription))
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        });
    }

    public async Task SetTags<T>(IEnumerable<KeyValuePair<string, T>> tags) where T : IEnumerable<string>
    {
        Dictionary<string, HashSet<string>> newTags = [];
        foreach (KeyValuePair<string, T> pair in tags)
        {
            string name = pair.Key.Trim();
            if (name.Length == 0)
            {
                continue;
            }

            List<string> processedTags = [];
            foreach (string tag in pair.Value)
            {
                string processedTag = tag.Trim();
                if (processedTag.Length == 0)
                {
                    continue;
                }

                processedTags.Add(processedTag);
            }

            if (processedTags.Count == 0)
            {
                continue;
            }

            if (!newTags.TryGetValue(name, out HashSet<string>? existingTags))
            {
                existingTags = [];
                newTags.Add(name, existingTags);
            }

            foreach (string tag in processedTags)
            {
                existingTags.Add(tag);
            }
        }

        _tags = new(newTags.ToDictionary(p => p.Key, p => new ComicTagCategory(p.Value)));

        await Enqueue(() =>
        {
            SaveNoLock(() =>
            {
                InternalSaveTagsNoLock();
            });
        });
    }

    public async Task SetLocation(string location)
    {
        Location = location;
        await Enqueue(() =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnLocation, GetColumnValue(ComicTable.ColumnLocation))
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        });
    }

    public async Task SetRating(int rating)
    {
        rating = Math.Clamp(rating, -1, 100);
        Rating = rating;
        await Enqueue(() =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnRating, GetColumnValue(ComicTable.ColumnRating))
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        });
    }

    private async Task SetPageCount(int pageCount)
    {
        if (PageCount == pageCount)
        {
            return;
        }

        PageCount = pageCount;
        await Enqueue(() =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnPageCount, GetColumnValue(ComicTable.ColumnPageCount))
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        });
    }

    public async Task SetProgress(int progress, double lastPosition)
    {
        Progress = Math.Clamp(progress, -1, 100);
        LastPosition = lastPosition;

        await Enqueue(() =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnProgress, GetColumnValue(ComicTable.ColumnProgress))
                    .AppendColumn(ComicTable.ColumnLastPosition, GetColumnValue(ComicTable.ColumnLastPosition))
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        });
    }

    public void MarkAsExternal()
    {
        Id = -1;
    }

    //
    // Comic Connection
    //

    public async Task<ComicConnection?> OpenComic()
    {
        BaseComicConnection? connection = await OpenComicConnection();
        if (connection is null)
        {
            return null;
        }

        bool connectionValid;
        try
        {
            connectionValid = await InitializeConnection(connection);
        }
        catch
        {
            connection.Dispose();
            throw;
        }

        if (!connectionValid)
        {
            connection.Dispose();
            return null;
        }

        return new ComicConnection(connection);
    }

    private async Task<bool> InitializeConnection(IComicConnection connection)
    {
        bool needFlushExt = false;

        // Refresh page count
        int pageCount = connection.ImageCount;
        if (pageCount <= 0)
        {
            Logger.F(TAG, "Comic connection has no images: " + Location);
            return false;
        }

        await SetPageCount(pageCount);

        // Refresh cover index
        string? coverIndexString = GetExt(ComicExt.COVER_INDEX);
        if (string.IsNullOrEmpty(coverIndexString) || !int.TryParse(coverIndexString, out int coverIndex) || coverIndex < 0 || coverIndex >= pageCount)
        {
            coverIndex = 0;
            SetExt(ComicExt.COVER_INDEX, coverIndex.ToString());
            needFlushExt = true;
        }

        // Refresh cover cache key
        string oldCoverCacheKey = GetExt(ComicExt.COVER_CACHE_KEY) ?? string.Empty;
        string newCoverCacheKey = connection.GetImageCacheKey(coverIndex);
        if (!string.IsNullOrEmpty(newCoverCacheKey) && oldCoverCacheKey != newCoverCacheKey)
        {
            SetExt(ComicExt.COVER_CACHE_KEY, newCoverCacheKey);
            needFlushExt = true;
        }

        if (needFlushExt)
        {
            await FlushExt();
        }

        return true;
    }

    //
    // Utilities
    //

    public async Task<bool> MoveToLocation(string newLocation)
    {
        return await MoveToLocationInternal(newLocation);
    }

    //
    // Virtual Methods
    //

    protected virtual Task<bool> MoveToLocationInternal(string newLocation)
    {
        return Task.FromResult(false);
    }

    //
    // Abstract Methods
    //

    public abstract IReadOnlyList<string> GetFolderViewPath();

    protected abstract Task<BaseComicConnection?> OpenComicConnection();

    //
    // DB Helpers
    //

    private static readonly Lazy<IReadOnlyList<IColumnTypeless>> _allNonIdColumns = new(() =>
    {
        return [
            ComicTable.ColumnType,
            ComicTable.ColumnLocation,
            ComicTable.ColumnTitle1,
            ComicTable.ColumnTitle2,
            ComicTable.ColumnHidden,
            ComicTable.ColumnRating,
            ComicTable.ColumnProgress,
            ComicTable.ColumnLastVisit,
            ComicTable.ColumnLastPosition,
            ComicTable.ColumnDescription,
            ComicTable.ColumnCompletionStatus,
            ComicTable.ColumnExt,
            ComicTable.ColumnPageCount,
        ];
    });

    private static readonly Lazy<IReadOnlyDictionary<string, Func<ComicHandle, object>>> _columnValueEvaluator = new(() =>
    {
        Dictionary<string, Func<ComicHandle, object>> evaluators = [];
        evaluators[ComicTable.ColumnId.Name] = i => TypeAssert.AssertLong(i.Id);
        evaluators[ComicTable.ColumnType.Name] = i => TypeAssert.AssertLong((long)i.Type);
        evaluators[ComicTable.ColumnLocation.Name] = i => TypeAssert.AssertString(i.Location);
        evaluators[ComicTable.ColumnTitle1.Name] = i => TypeAssert.AssertString(i.Title1);
        evaluators[ComicTable.ColumnTitle2.Name] = i => TypeAssert.AssertString(i.Title2);
        evaluators[ComicTable.ColumnHidden.Name] = i => TypeAssert.AssertBoolean(i.Hidden);
        evaluators[ComicTable.ColumnRating.Name] = i => TypeAssert.AssertInt(i.Rating);
        evaluators[ComicTable.ColumnProgress.Name] = i => TypeAssert.AssertInt(i.Progress);
        evaluators[ComicTable.ColumnLastVisit.Name] = i => TypeAssert.AssertDateTimeOffset(i.LastVisit);
        evaluators[ComicTable.ColumnLastPosition.Name] = i => TypeAssert.AssertDouble(i.LastPosition);
        evaluators[ComicTable.ColumnDescription.Name] = i => TypeAssert.AssertString(i.Description);
        evaluators[ComicTable.ColumnCompletionStatus.Name] = i => TypeAssert.AssertInt((int)i.CompletionStatus);
        evaluators[ComicTable.ColumnExt.Name] = i => TypeAssert.AssertString(JsonSerializer.Serialize(i._ext));
        evaluators[ComicTable.ColumnPageCount.Name] = i => TypeAssert.AssertInt(i.PageCount);
        return evaluators;
    });

    private object GetColumnValue(IColumnTypeless column)
    {
        Func<ComicHandle, object> evaluator = _columnValueEvaluator.Value[column.Name];
        return evaluator(this);
    }

    //
    // Unsorted
    //

    public async Task SaveHiddenAsync(bool hidden)
    {
        Hidden = hidden;

        await Enqueue(() =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnHidden, GetColumnValue(ComicTable.ColumnHidden))
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        });
    }

    public async Task SaveCompletionStatus(CompletionStatusEnum completionState)
    {
        CompletionStatus = completionState;

        await Enqueue(() =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnCompletionStatus, GetColumnValue(ComicTable.ColumnCompletionStatus))
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        });
    }

    public void SetAsVisited()
    {
        LastVisit = DateTimeOffset.Now;
        Progress = Math.Max(Progress, 0);

        CoroutineUtils.Run(() => Enqueue(() =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnProgress, GetColumnValue(ComicTable.ColumnProgress))
                    .AppendColumn(ComicTable.ColumnLastVisit, GetColumnValue(ComicTable.ColumnLastVisit))
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        }));
    }

    public void SetAsDefaultInfo()
    {
        List<string> subPaths = [.. Location.Split(ArchiveManager.ARCHIVE_SEP)];
        var tags = new List<string>();
        foreach (string path in subPaths)
        {
            List<string> subTags = [.. path.Split('\\')];
            if (subTags.Count == 0)
            {
                continue;
            }

            if (Type != ComicType.Folder)
            {
                subTags[^1] = StringUtils.DisplayNameFromFilename(subTags[^1]);
            }

            tags.AddRange(subTags);
        }

        Title1 = tags.Count >= 1 ? tags[^1] : string.Empty;
        Title2 = string.Empty;

        if (tags.Count >= 2)
        {
            _tags = new(new Dictionary<string, ComicTagCategory>
            {
                { StringResourceProvider.Instance.Default, new ComicTagCategory([.. tags[1..^1]]) }
            });
        }
        else
        {
            _tags = new([]);
        }
    }

    private void InternalSaveTagsNoLock()
    {
        DeleteCommand.Create(TagCategoryTable.Instance)
            .AppendCondition(TagCategoryTable.ColumnComicId, Id)
            .Execute();

        foreach (KeyValuePair<string, ComicTagCategory> item in Tags)
        {
            long tagCategoryId = InsertCommand.Create(TagCategoryTable.Instance)
                .AppendColumn(TagCategoryTable.ColumnName, item.Key)
                .AppendColumn(TagCategoryTable.ColumnComicId, Id)
                .Execute();

            foreach (string tag in item.Value.Tags)
            {
                InsertCommand.Create(TagTable.Instance)
                    .AppendColumn(TagTable.ColumnContent, tag)
                    .AppendColumn(TagTable.ColumnComicId, Id)
                    .AppendColumn(TagTable.ColumnTagCategoryId, tagCategoryId)
                    .Execute();
            }
        }
    }

    private void SaveNoLock(Action action)
    {
        if (IsExternal)
        {
            return;
        }

        if (Id < 0)
        {
            Logger.F(TAG, "SaveNoLock: Cannot save comic with invalid id");
            return;
        }

        action();
    }

    //
    // Types
    //

    private partial class ReadOnlyTags(Dictionary<string, ComicTagCategory> source) :
        IReadOnlyDictionary<string, ComicTagCategory>,
        IReadOnlyDictionary<string, SDK.Plugins.Comic.IComicTagCategory>
    {
        ComicTagCategory IReadOnlyDictionary<string, ComicTagCategory>.this[string key] => source[key];

        SDK.Plugins.Comic.IComicTagCategory IReadOnlyDictionary<string, SDK.Plugins.Comic.IComicTagCategory>.this[string key] => source[key];

        IEnumerable<string> IReadOnlyDictionary<string, ComicTagCategory>.Keys => source.Keys;

        IEnumerable<string> IReadOnlyDictionary<string, SDK.Plugins.Comic.IComicTagCategory>.Keys => source.Keys;

        IEnumerable<ComicTagCategory> IReadOnlyDictionary<string, ComicTagCategory>.Values => source.Values;

        IEnumerable<SDK.Plugins.Comic.IComicTagCategory> IReadOnlyDictionary<string, SDK.Plugins.Comic.IComicTagCategory>.Values => source.Values;

        int IReadOnlyCollection<KeyValuePair<string, ComicTagCategory>>.Count => source.Count;

        int IReadOnlyCollection<KeyValuePair<string, SDK.Plugins.Comic.IComicTagCategory>>.Count => source.Count;

        bool IReadOnlyDictionary<string, ComicTagCategory>.ContainsKey(string key)
        {
            return source.ContainsKey(key);
        }

        bool IReadOnlyDictionary<string, SDK.Plugins.Comic.IComicTagCategory>.ContainsKey(string key)
        {
            return source.ContainsKey(key);
        }

        IEnumerator<KeyValuePair<string, ComicTagCategory>> IEnumerable<KeyValuePair<string, ComicTagCategory>>.GetEnumerator()
        {
            return source.GetEnumerator();
        }

        IEnumerator<KeyValuePair<string, SDK.Plugins.Comic.IComicTagCategory>> IEnumerable<KeyValuePair<string, SDK.Plugins.Comic.IComicTagCategory>>.GetEnumerator()
        {
            foreach ((string? key, ComicTagCategory? value) in source)
            {
                yield return new(key, value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return source.GetEnumerator();
        }

        bool IReadOnlyDictionary<string, ComicTagCategory>.TryGetValue(string key, [MaybeNullWhen(false)] out ComicTagCategory value)
        {
            return source.TryGetValue(key, out value);
        }

        bool IReadOnlyDictionary<string, SDK.Plugins.Comic.IComicTagCategory>.TryGetValue(string key, [MaybeNullWhen(false)] out SDK.Plugins.Comic.IComicTagCategory value)
        {
            bool result = source.TryGetValue(key, out ComicTagCategory? tempValue);
            value = tempValue;
            return result;
        }
    }

    private struct UpdateItemInfo
    {
        public string Location;
        public ComicType ItemType;
    };

    private class TagTempData
    {
        public long ComicId = -1;
        public string Name = "";
        public HashSet<string> Tags = [];
    }

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

    //
    // Enums
    //

    internal enum ComicType : int
    {
        Folder = 1,
        Archive = 2,
        PDF = 3,
    }
};
