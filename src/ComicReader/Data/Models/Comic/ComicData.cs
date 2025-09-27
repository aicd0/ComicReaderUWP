// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Legacy;
using ComicReader.Common.Lifecycle;
using ComicReader.Common.Utils;
using ComicReader.Data.Tables;
using ComicReader.SDK.Common.AutoProperty;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Threading;
using ComicReader.SDK.Data.SqlHelpers;

namespace ComicReader.Data.Models.Comic;

internal abstract class ComicData
{
    //
    // Constants
    //

    private const string TAG = "ComicData";
    private const int COVER_INDEX = 0;

    //
    // Static Variables
    //

    private static readonly MutableLiveData<bool> _isScanningLibrary = new(false);
    public static LiveData<bool> IsScanningLibrary => _isScanningLibrary;

    private static string? _defaultTagsString = null;
    private static string DefaultTagsString
    {
        get
        {
            _defaultTagsString ??= StringResourceProvider.Instance.DefaultTags;
            return _defaultTagsString;
        }
    }

    private static int _pendingUpdateTaskCount = 0;

    //
    // Static Methods
    //

    public static async Task<T> Enqueue<T>(string taskName, Func<T> op)
    {
        var taskResult = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        SqlDatabaseManager.MainDatabaseDispatcher.Submit($"{TAG}#Enqueue#{taskName}", delegate
        {
            taskResult.SetResult(op());
        });

        return await taskResult.Task;
    }

    public static async Task<ComicData?> FromId(long id, string taskName)
    {
        return await Enqueue(taskName, delegate
        {
            return FromIdNoLock(id);
        });
    }

    public static async Task<ComicData?> FromLocation(string location, string taskName)
    {
        return await Enqueue(taskName, delegate
        {
            return FromLocationNoLock(location);
        });
    }

    public static async Task<List<ComicData>> BatchFromId(IEnumerable<long> ids, string taskName)
    {
        return await Enqueue(taskName, delegate
        {
            return BatchFromIdNoLock(ids);
        });
    }

    private static ComicData? FromIdNoLock(long id)
    {
        List<ComicData> result = BatchFromIdNoLock([id]);
        if (result.Count == 0)
        {
            return null;
        }
        return result[0];
    }

    private static List<ComicData> BatchFromIdNoLock(IEnumerable<long> ids)
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

        Dictionary<long, ComicData> comics = new(ids.Count());
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
            IReaderToken<string> coverCacheKeyToken = command.PutQueryString(ComicTable.ColumnCoverCacheKey);
            IReaderToken<string> descriptionToken = command.PutQueryString(ComicTable.ColumnDescription);
            IReaderToken<int> completionStateToken = command.PutQueryInt32(ComicTable.ColumnCompletionState);
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
                string coverCacheKey = coverCacheKeyToken.GetValue();
                string description = descriptionToken.GetValue();
                ComicCompletionStatusEnum completionState = ComicPropertyRepository.ParseCompletionState(completionStateToken.GetValue());
                int pageCount = pageCountToken.GetValue();
                string extJson = extToken.GetValue();

                ComicData? comic = FromDatabase(type, location);
                if (comic == null)
                {
                    continue;
                }

                comic.Id = id;
                comic.Title1 = title1;
                comic.Title2 = title2;
                comic.Hidden = hidden;
                comic.Rating = rating;
                comic.Progress = progress;
                comic.LastVisit = lastVisit;
                comic.LastPosition = lastPosition;
                comic.CoverCacheKey = coverCacheKey;
                comic.Description = description;
                comic.Tags = [];
                comic.CompletionState = completionState;
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
                        Logger.AssertNotReachHere("", ex);
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
            Dictionary<long, List<TagData>> comicTags = [];
            foreach (TagTempData tagCategory in tagCategories.Values)
            {
                if (comics.TryGetValue(tagCategory.ComicId, out ComicData? comic))
                {
                    TagData tagData = new(tagCategory.Name, tagCategory.Tags);
                    if (!comicTags.TryGetValue(tagCategory.ComicId, out List<TagData>? tags))
                    {
                        tags = [];
                        comicTags[tagCategory.ComicId] = tags;
                    }
                    tags.Add(tagData);
                }
            }
            foreach (KeyValuePair<long, List<TagData>> pair in comicTags)
            {
                if (comics.TryGetValue(pair.Key, out ComicData? comic))
                {
                    comic.Tags = pair.Value;
                }
            }
        }

        return [.. comics.Values];
    }

    private static ComicData? FromLocationNoLock(string location)
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

    private static ComicData? FromDatabase(ComicType type, string location)
    {
        switch (type)
        {
            case ComicType.Folder:
                return ComicFolderData.FromDatabase(location);
            case ComicType.Archive:
                return ComicArchiveData.FromDatabase(location);
            case ComicType.PDF:
                return ComicPdfData.FromDatabase(location);
            default:
                Logger.AssertNotReachHere("419CBCB3E803A525");
                return null;
        }
    }

    protected static void Log(string message)
    {
        Logger.I("ComicData", message);
    }

    //
    // Member variables
    //

    private readonly ConcurrentDictionary<string, string> _ext = [];
    private bool _imageUpdated = false;

    //
    // Properties
    //

    public long Id { get; private set; } = -1;
    public ComicCompletionStatusEnum CompletionState { get; private set; }
    public string Location { get; protected set; } = "";
    public string Title1 { get; protected set; } = "";
    public string Title2 { get; protected set; } = "";
    public bool Hidden { get; protected set; } = false;
    public int Rating { get; protected set; } = -1;
    public int Progress { get; protected set; } = -1;
    public DateTimeOffset LastVisit { get; protected set; } = DateTimeOffset.MinValue;
    public double LastPosition { get; protected set; } = 0.0;
    public string CoverCacheKey { get; private set; } = "";
    public string Description { get; private set; } = "";
    public IReadOnlyList<TagData> Tags { get; private set; } = [];
    public int PageCount { get; private set; } = -1;

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

    public bool IsExternal { get; private set; }
    public abstract bool IsEditable { get; }
    public virtual string FileExplorerPath => Location;

    private ComicType Type { get; set; }

    private ComicType ValueType => Type;
    private string ValueLocation => Location;
    private string ValueTitle1 => Title1;
    private string ValueTitle2 => Title2;
    private bool ValueHidden => Hidden;
    private int ValueRating => Rating;
    private int ValueProgress => Progress;
    private DateTimeOffset ValueLastVisit => LastVisit;
    private double ValueLastPosition => LastPosition;
    private string ValueCoverCacheKey => CoverCacheKey;
    private string ValueDescription => Description;
    private string ValueExt => JsonSerializer.Serialize(_ext);
    private int ValuePageCount => PageCount;

    //
    // Constructor
    //

    protected ComicData(ComicType type, bool is_external)
    {
        Id = -1;
        Type = type;
        IsExternal = is_external;
    }

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

    public void FlushExt()
    {
        _ = Enqueue("FlushExt", () =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnExt, ValueExt)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
            return true;
        });
    }

    public void SetTitle1(string title)
    {
        Title1 = title;
        _ = Enqueue("SetTitle1", () =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnTitle1, ValueTitle1)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
            return true;
        });
    }

    public void SetTitle2(string title)
    {
        Title2 = title;
        _ = Enqueue("SetTitle2", () =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnTitle2, ValueTitle2)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
            return true;
        });
    }

    public void SetDescription(string description)
    {
        Description = description;
        _ = Enqueue("SetDescription", () =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnDescription, ValueDescription)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
            return true;
        });
    }

    public void SetTags(IReadOnlyDictionary<string, HashSet<string>> tags)
    {
        List<TagData> newTags = [];
        foreach (KeyValuePair<string, HashSet<string>> pair in tags)
        {
            string name = pair.Key.Trim();
            if (name.Length == 0)
            {
                continue;
            }

            HashSet<string> processedTags = [];
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

            TagData tagData = new(name, processedTags);
            newTags.Add(tagData);
        }

        Tags = newTags;

        _ = Enqueue("SetTags", () =>
        {
            SaveNoLock(() =>
            {
                InternalSaveTagsNoLock();
            });
            return true;
        });
    }

    public void SetLocation(string location)
    {
        Location = location;
        _ = Enqueue("SetLocation", () =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnLocation, ValueLocation)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
            return true;
        });
    }

    private void SetPageCount(int pageCount)
    {
        if (PageCount == pageCount)
        {
            return;
        }

        PageCount = pageCount;
        _ = Enqueue("SetPageCount", () =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnPageCount, ValuePageCount)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
            return true;
        });
    }

    //
    // Comic Connection
    //

    public async Task<IComicConnection?> OpenComicAsync()
    {
        IComicConnection? connection = await OpenComicConnection();
        if (connection is null)
        {
            return null;
        }

        try
        {
            int pageCount = connection.GetImageCount();
            if (pageCount > 0)
            {
                SetPageCount(pageCount);
            }
            else
            {
                Logger.F(TAG, "OpenComicAsync: Comic has zero images.");
            }
        }
        catch
        {
            connection.Dispose();
            throw;
        }

        return new ComicConnectionWrapper(connection);
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

    protected abstract Task<IComicConnection?> OpenComicConnection();

    //
    // Unsorted
    //

    private void SaveAllNoLock()
    {
        SaveNoLock(delegate
        {
            UpdateCommand.Create(ComicTable.Instance)
                .AppendColumn(ComicTable.ColumnType, (long)ValueType)
                .AppendColumn(ComicTable.ColumnLocation, ValueLocation)
                .AppendColumn(ComicTable.ColumnTitle1, ValueTitle1)
                .AppendColumn(ComicTable.ColumnTitle2, ValueTitle2)
                .AppendColumn(ComicTable.ColumnHidden, ValueHidden)
                .AppendColumn(ComicTable.ColumnRating, ValueRating)
                .AppendColumn(ComicTable.ColumnProgress, ValueProgress)
                .AppendColumn(ComicTable.ColumnLastVisit, ValueLastVisit)
                .AppendColumn(ComicTable.ColumnLastPosition, ValueLastPosition)
                .AppendColumn(ComicTable.ColumnCoverCacheKey, ValueCoverCacheKey)
                .AppendColumn(ComicTable.ColumnDescription, ValueDescription)
                .AppendColumn(ComicTable.ColumnExt, ValueExt)
                .AppendColumn(ComicTable.ColumnPageCount, ValuePageCount)
                .AppendCondition(ComicTable.ColumnId, Id)
                .Execute();
            InternalSaveTagsNoLock();
        });
    }

    public async Task SaveHiddenAsync(bool hidden)
    {
        Hidden = hidden;

        await Enqueue("SaveHiddenAsync", delegate
        {
            SaveNoLock(delegate
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnHidden, ValueHidden)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
            return true;
        });
    }

    public Task SaveCompletionState(ComicCompletionStatusEnum completionState)
    {
        CompletionState = completionState;
        return ComicPropertyRepository.Instance.CompletionStateOperator.Write(Id, completionState, CreateRequestOption());
    }

    public void SaveRating(int rating)
    {
        Rating = rating;

        _ = Enqueue("SaveRating", delegate
        {
            SaveNoLock(delegate
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnRating, ValueRating)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
            return true;
        });
    }

    public async Task SaveProgressAsync(int progress, double last_position)
    {
        Progress = progress;
        LastPosition = last_position;

        await Enqueue("SaveProgress", delegate
        {
            SaveNoLock(delegate
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnProgress, ValueProgress)
                    .AppendColumn(ComicTable.ColumnLastPosition, ValueLastPosition)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
            return true;
        });
    }

    public void SetAsStarted()
    {
        LastVisit = DateTimeOffset.Now;
        Progress = Math.Max(Progress, 0);

        _ = Enqueue("SetAsRead", delegate
        {
            SaveNoLock(delegate
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnProgress, ValueProgress)
                    .AppendColumn(ComicTable.ColumnLastVisit, ValueLastVisit)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
            return true;
        });
    }

    public void SetCoverCacheKey(string key)
    {
        CoverCacheKey = key;

        _ = Enqueue("SetCoverCacheKey", delegate
        {
            SaveNoLock(delegate
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnCoverCacheKey, ValueCoverCacheKey)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
            return true;
        });
    }

    public void SetAsDefaultInfo()
    {
        Title1 = "";
        Title2 = "";

        List<string> sub_paths = [.. Location.Split(ArchiveAccess.FileSeperator)];
        var tags = new List<string>();

        foreach (string path in sub_paths)
        {
            List<string> sub_tags = [.. path.Split('\\')];

            if (sub_tags.Count == 0)
            {
                continue;
            }

            if (Type != ComicType.Folder)
            {
                sub_tags[sub_tags.Count - 1] = StringUtils.DisplayNameFromFilename(sub_tags[sub_tags.Count - 1]);
            }

            tags.AddRange(sub_tags);
        }

        if (tags.Count <= 1)
        {
            return;
        }

        Title1 = tags[tags.Count - 1];

        TagData defaultTag = new(DefaultTagsString, tags.Skip(1).ToHashSet());
        Tags = [defaultTag];
    }

    public async Task<bool> LoadImageFiles()
    {
        if (_imageUpdated)
        {
            return true;
        }

        if (!await ReloadImages())
        {
            return false;
        }

        _imageUpdated = true;

        using IComicConnection? connection = await OpenComicAsync();
        if (connection == null)
        {
            return false;
        }

        if (connection.GetImageCount() == 0)
        {
            return false;
        }

        return true;
    }

    public async Task<bool> ReloadImageFiles()
    {
        _imageUpdated = false;
        bool success = await LoadImageFiles();
        if (success)
        {
            // Refresh cover cache key
            string newCoverCacheKey = GetImageCacheKey(COVER_INDEX);
            if (!string.IsNullOrEmpty(newCoverCacheKey) && CoverCacheKey != newCoverCacheKey)
            {
                SetCoverCacheKey(newCoverCacheKey);
            }
        }

        return success;
    }

    public string GetCoverImageCacheKey()
    {
        string coverCacheKey = CoverCacheKey;
        if (!string.IsNullOrEmpty(coverCacheKey))
        {
            return coverCacheKey;
        }

        if (!LoadImageFiles().Result)
        {
            return string.Empty;
        }

        coverCacheKey = GetImageCacheKey(COVER_INDEX);
        SetCoverCacheKey(coverCacheKey);
        return coverCacheKey;
    }

    public static void UpdateAllComics(string reason, bool skipExistingLocation)
    {
        int pendingCount = Interlocked.Increment(ref _pendingUpdateTaskCount);
        Log($"UpdateAllComics#Enqueue(reason={reason},skipExistingLocation={skipExistingLocation})");
        TaskDispatcher.LongRunningThreadPool.Submit($"{TAG}#UpdateAllComics", delegate
        {
            int pendingCount = Interlocked.Decrement(ref _pendingUpdateTaskCount);
            if (pendingCount > 0)
            {
                return;
            }

            _isScanningLibrary.Emit(true);
            try
            {
                UpdateAllComicsInternal(skipExistingLocation).Wait();
            }
            finally
            {
                _isScanningLibrary.Emit(false);
            }

            DispatchComicUpdateEvent();
        });
    }

    public abstract string GetImageCacheKey(int index);

    public abstract int GetImageSignature(int index);

    protected abstract Task<bool> ReloadImages();

    private static void UpdateComicNoLock(string location, ComicType type, bool is_exist)
    {
        Log((is_exist ? "Updat" : "Add") + "ing comic '" + location + "'");

        // Update or create a new one.
        ComicData? comic;

        if (is_exist)
        {
            comic = FromLocationNoLock(location);
        }
        else
        {
            comic = FromDatabase(type, location);
        }

        if (comic == null)
        {
            return;
        }

        // Load comic info locally.
        if (!is_exist)
        {
            comic.SetAsDefaultInfo();
            comic.SaveAllNoLock();
        }
    }

    private void InternalSaveTagsNoLock(bool removeOld = true)
    {
        if (removeOld)
        {
            DeleteCommand.Create(TagCategoryTable.Instance)
                .AppendCondition(TagCategoryTable.ColumnComicId, Id)
                .Execute();
        }

        foreach (TagData category in Tags)
        {
            long tagCategoryId = InsertCommand.Create(TagCategoryTable.Instance)
                .AppendColumn(TagCategoryTable.ColumnName, category.Name)
                .AppendColumn(TagCategoryTable.ColumnComicId, Id)
                .Execute();

            foreach (string tag in category.Tags)
            {
                InsertCommand.Create(TagTable.Instance)
                    .AppendColumn(TagTable.ColumnContent, tag)
                    .AppendColumn(TagTable.ColumnComicId, Id)
                    .AppendColumn(TagTable.ColumnTagCategoryId, tagCategoryId)
                    .Execute();
            }
        }
    }

    private void InternalInsertNoLock()
    {
        Id = InsertCommand.Create(ComicTable.Instance)
            .AppendColumn(ComicTable.ColumnType, (long)ValueType)
            .AppendColumn(ComicTable.ColumnLocation, ValueLocation)
            .AppendColumn(ComicTable.ColumnTitle1, ValueTitle1)
            .AppendColumn(ComicTable.ColumnTitle2, ValueTitle2)
            .AppendColumn(ComicTable.ColumnHidden, ValueHidden)
            .AppendColumn(ComicTable.ColumnRating, ValueRating)
            .AppendColumn(ComicTable.ColumnProgress, ValueProgress)
            .AppendColumn(ComicTable.ColumnLastVisit, ValueLastVisit)
            .AppendColumn(ComicTable.ColumnLastPosition, ValueLastPosition)
            .AppendColumn(ComicTable.ColumnCoverCacheKey, CoverCacheKey)
            .AppendColumn(ComicTable.ColumnDescription, Description)
            .AppendColumn(ComicTable.ColumnCompletionState, CompletionState)
            .AppendColumn(ComicTable.ColumnExt, ValueExt)
            .AppendColumn(ComicTable.ColumnPageCount, ValuePageCount)
            .Execute();

        InternalSaveTagsNoLock(removeOld: false);
    }

    private void SaveNoLock(Action action)
    {
        if (IsExternal)
        {
            return;
        }

        if (Id < 0)
        {
            InternalInsertNoLock();
            return;
        }

        action();
    }

    private static async Task TransactionBlock(Func<Task> op, string taskName)
    {
        await Enqueue(taskName, delegate
        {
            SqlDatabaseManager.MainDatabase.WithTransaction(() =>
            {
                op().Wait();
            });
            return true;
        });
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

    private static async Task UpdateAllComicsInternal(bool skipExistingLocation)
    {
        AppSettingsModel.ExternalModel appSettings = AppSettingsModel.Instance.GetModel();

        // Fetch all locations in the database
        var oldLocations = new List<string>();
        await Enqueue("GetLocationsFromDatabase", delegate
        {
            var command = SelectCommand.Create(ComicTable.Instance);
            IReaderToken<string> locationToken = command.PutQueryString(ComicTable.ColumnLocation);
            using SelectCommand.IReader reader = command.Execute();
            while (reader.Read())
            {
                oldLocations.Add(locationToken.GetValue());
            }

            return true;
        });

        // Get all root folders from setting
        List<string> rootFolders = [];
        foreach (string path in AppSettingsModel.Instance.GetModel().ComicFolders)
        {
            rootFolders.Add(path);
        }

        // Scan all root folders
        var newLocations = new List<string>();
        var noAccessLocations = new List<string>();
        var watch = new Stopwatch();
        watch.Start();
        foreach (string folderPath in rootFolders)
        {
            Logger.I(TAG, $"Scanning: {folderPath}");
            if (!Directory.Exists(folderPath))
            {
                Logger.I(TAG, $"Folder not exists, skipped: {folderPath}");
                continue;
            }

            var ctx = new SearchContext(folderPath, PathType.Folder);
            while (await ctx.Search(1024))
            {
                if (_pendingUpdateTaskCount > 0)
                {
                    return;
                }

                Logger.I(TAG, $"Scanning {ctx.ItemFound} files/folders...");
                var scanResult = new Dictionary<string, ComicType>();
                foreach (string filePath in ctx.Files)
                {
                    string filename = StringUtils.ItemNameFromPath(filePath);
                    string extension = StringUtils.ExtensionFromFilename(filename).ToLower();
                    if (AppInfoProvider.IsSupportedImageExtension(extension))
                    {
                        string parentPath = StringUtils.ParentLocationFromLocation(filePath);
                        if (!scanResult.ContainsKey(parentPath))
                        {
                            scanResult[parentPath] =
                                ArchiveAccess.IsArchivePath(filePath) ?
                                ComicType.Archive : ComicType.Folder;
                        }
                    }
                    else
                    {
                        switch (extension)
                        {
                            case ".pdf":
                                scanResult[filePath] = ComicType.PDF;
                                break;
                            default:
                                break;
                        }
                    }
                }

                List<string> incrementNewLocations = [];
                incrementNewLocations.AddRange(scanResult.Keys);
                newLocations.AddRange(incrementNewLocations);
                noAccessLocations.AddRange(ctx.NoAccessItems);

                // Update comics
                var queue = new List<UpdateItemInfo>();

                var locationAdded = C3<string, string, string>.Except(
                    incrementNewLocations, oldLocations,
                    StringUtils.UniquePath, StringUtils.UniquePath,
                    new C1<string>.DefaultEqualityComparer()).ToList();
                foreach (string location in locationAdded)
                {
                    queue.Add(new UpdateItemInfo
                    {
                        Location = location,
                        ItemType = scanResult[location],
                        IsExist = false,
                    });
                }

                if (!skipExistingLocation)
                {
                    var locationKept = C3<string, string, string>.Intersect(
                        incrementNewLocations, oldLocations,
                        StringUtils.UniquePath, StringUtils.UniquePath,
                        new C1<string>.DefaultEqualityComparer()).ToList();
                    foreach (string location in locationKept)
                    {
                        queue.Add(new UpdateItemInfo
                        {
                            Location = location,
                            ItemType = scanResult[location],
                            IsExist = true,
                        });
                    }
                }

                await TransactionBlock(async delegate
                {
                    foreach (UpdateItemInfo info in queue)
                    {
                        UpdateComicNoLock(info.Location, info.ItemType, info.IsExist);
                    }
                    await Task.CompletedTask;
                }, "UpdateComic");

                if (watch.LapSpan().TotalSeconds > 2)
                {
                    DispatchComicUpdateEvent();
                    watch.Lap();
                }
            }
        }

        if (appSettings.RemoveUnreachableComics)
        {
            var locationRemoved = C3<string, string, string>.Except(
                oldLocations, newLocations,
                StringUtils.UniquePath, StringUtils.UniquePath,
                new C1<string>.DefaultEqualityComparer()).ToList();

            // Skip no access directories
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
                await TransactionBlock(delegate
                {
                    foreach (string location in locationRemoved)
                    {
                        Logger.I(TAG, $"Removing: {location}");
                        RemoveWithLocationNoLock(location);
                    }

                    return Task.CompletedTask;
                }, "RemoveLocationsFromDatabase");
            }
        }
    }

    private RequestOption CreateRequestOption()
    {
        return new(!IsExternal);
    }

    //
    // Helper Methods
    //

    private static void DispatchComicUpdateEvent()
    {
        GlobalEvent.Instance.ComicUpdated.Emit(0);
    }

    //
    // Types
    //

    private struct UpdateItemInfo
    {
        public string Location;
        public ComicType ItemType;
        public bool IsExist;
    };

    internal class TagData(string name, IEnumerable<string> tags)
    {
        public readonly string Name = name;
        public readonly IReadOnlySet<string> Tags = (HashSet<string>)[.. tags];
    };

    private class TagTempData
    {
        public long ComicId = -1;
        public string Name = "";
        public HashSet<string> Tags = [];
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
