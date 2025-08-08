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
        {
            SelectCommand command = SelectCommand.Create(ComicTable.Instance)
                .AppendCondition(new InCondition(ColumnOrValue.FromColumn(ComicTable.ColumnId), ids));
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
        {
            SelectCommand command = SelectCommand.Create(TagCategoryTable.Instance)
                .AppendCondition(new InCondition(ColumnOrValue.FromColumn(TagCategoryTable.ColumnComicId), comics.Keys));
            IReaderToken<long> comicIdToken = command.PutQueryInt64(TagCategoryTable.ColumnComicId);
            IReaderToken<long> tagCategoryIdToken = command.PutQueryInt64(TagCategoryTable.ColumnId);
            IReaderToken<string> nameToken = command.PutQueryString(TagCategoryTable.ColumnName);
            using SelectCommand.IReader reader = command.Execute();

            while (reader.Read())
            {
                long comicId = comicIdToken.GetValue();
                if (comics.TryGetValue(comicId, out ComicData? comic))
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

        {
            SelectCommand command = SelectCommand.Create(TagTable.Instance)
                .AppendCondition(new InCondition(ColumnOrValue.FromColumn(TagTable.ColumnTagCategoryId), tagCategories.Keys));
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
    private ComicType Type { get; set; }
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
            return SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnExt, ValueExt)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        });
    }

    public void SetTitle1(string title)
    {
        Title1 = title;
        _ = Enqueue("SetTitle1", () =>
        {
            return SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnTitle1, ValueTitle1)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        });
    }

    public void SetTitle2(string title)
    {
        Title2 = title;
        _ = Enqueue("SetTitle2", () =>
        {
            return SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnTitle2, ValueTitle2)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        });
    }

    public void SetDescription(string description)
    {
        Description = description;
        _ = Enqueue("SetDescription", () =>
        {
            return SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnDescription, ValueDescription)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
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
            return SaveNoLock(() =>
            {
                InternalSaveTagsNoLock();
            });
        });
    }

    public async Task<bool> MoveToLocation(string newLocation)
    {
        bool success = await MoveToLocationInternal(newLocation);
        if (!success)
        {
            return false;
        }

        Location = newLocation;

        _ = Enqueue("MoveToLocation", () =>
        {
            return SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnLocation, ValueLocation)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        });
        return true;
    }

    //
    // Virtual Methods
    //

    protected virtual Task<bool> MoveToLocationInternal(string newLocation)
    {
        return Task.FromResult(false);
    }

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
            return SaveNoLock(delegate
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnHidden, ValueHidden)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
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
            return SaveNoLock(delegate
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnRating, ValueRating)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        });
    }

    public async Task SaveProgressAsync(int progress, double last_position)
    {
        Progress = progress;
        LastPosition = last_position;

        await Enqueue("SaveProgress", delegate
        {
            return SaveNoLock(delegate
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnProgress, ValueProgress)
                    .AppendColumn(ComicTable.ColumnLastPosition, ValueLastPosition)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        });
    }

    public void SetAsStarted()
    {
        LastVisit = DateTimeOffset.Now;
        Progress = Math.Max(Progress, 0);

        _ = Enqueue("SetAsRead", delegate
        {
            return SaveNoLock(delegate
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnProgress, ValueProgress)
                    .AppendColumn(ComicTable.ColumnLastVisit, ValueLastVisit)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
        });
    }

    public void SetCoverCacheKey(string key)
    {
        CoverCacheKey = key;

        _ = Enqueue("SetCoverCacheKey", delegate
        {
            return SaveNoLock(delegate
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnCoverCacheKey, ValueCoverCacheKey)
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
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

        TaskException result = await ReloadImages();
        if (!result.Successful())
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
        return await LoadImageFiles();
    }

    public string GetCoverImageCacheKey()
    {
        string coverCacheKey = CoverCacheKey;
        if (coverCacheKey != null && coverCacheKey.Length > 0)
        {
            return coverCacheKey;
        }

        if (!LoadImageFiles().Result)
        {
            return string.Empty;
        }

        coverCacheKey = GetImageCacheKey(0);
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

    public abstract Task<IComicConnection?> OpenComicAsync();

    protected abstract Task<TaskException> ReloadImages();

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
            .Execute();

        InternalSaveTagsNoLock(removeOld: false);
    }

    private TaskException SaveNoLock(Action action)
    {
        if (IsExternal)
        {
            return TaskException.Success;
        }

        if (Id < 0)
        {
            InternalInsertNoLock();
            return TaskException.Success;
        }

        action();
        return TaskException.Success;
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
        DeleteCommand.Create(ComicTable.Instance)
            .AppendCondition(new LikeCondition(ComicTable.ColumnLocation, location + "%"))
            .Execute();
    }

    private static async Task<TaskException> UpdateAllComicsInternal(bool skipExistingLocation)
    {
        AppSettingsModel.ExternalModel appSettings = AppSettingsModel.Instance.GetModel();

        // Fetch all locations in the database
        var locExist = new List<string>();
        await Enqueue("GetLocationsFromDatabase", delegate
        {
            var command = SelectCommand.Create(ComicTable.Instance);
            IReaderToken<string> locationToken = command.PutQueryString(ComicTable.ColumnLocation);
            using SelectCommand.IReader reader = command.Execute();
            while (reader.Read())
            {
                locExist.Add(locationToken.GetValue());
            }
            return true;
        });

        // Get all root folders from setting
        List<string> rootFolders = [];
        foreach (string path in AppSettingsModel.Instance.GetModel().ComicFolders)
        {
            rootFolders.Add(path);
        }

        // Get all subfolders in root folders
        var locInLib = new List<string>();
        var locIgnore = new List<string>();
        var watch = new Stopwatch();
        watch.Start();

        foreach (string folderPath in rootFolders)
        {
            Log("Scanning folder '" + folderPath + "'");

            // Skip unreachable folders from database
            if (!Directory.Exists(folderPath))
            {
                Log("Failed to reach folder '" + folderPath + "', skipped");
                continue;
            }

            var ctx = new SearchContext(folderPath, PathType.Folder);
            while (await ctx.Search(1024))
            {
                // Cancel this task if more requests have come in
                if (_pendingUpdateTaskCount > 0)
                {
                    return TaskException.Cancellation;
                }

                Log("Scanning " + ctx.ItemFound.ToString() + " items.");
                var locScannedDict = new Dictionary<string, ComicType>();

                foreach (string file_path in ctx.Files)
                {
                    string filename = StringUtils.ItemNameFromPath(file_path);
                    string extension = StringUtils.ExtensionFromFilename(filename).ToLower();

                    if (AppInfoProvider.IsSupportedImageExtension(extension))
                    {
                        string loc = StringUtils.ParentLocationFromLocation(file_path);

                        if (!locScannedDict.ContainsKey(loc))
                        {
                            locScannedDict[loc] =
                                ArchiveAccess.IsArchivePath(file_path) ?
                                ComicType.Archive : ComicType.Folder;
                        }
                    }
                    else
                    {
                        switch (extension)
                        {
                            case ".pdf":
                                locScannedDict[file_path] = ComicType.PDF;
                                break;
                            default:
                                break;
                        }
                    }
                }

                var locScanned = new List<string>();
                foreach (KeyValuePair<string, ComicType> item in locScannedDict)
                {
                    locScanned.Add(item.Key);
                }
                locInLib.AddRange(locScanned);
                foreach (string dir in ctx.NoAccessItems)
                {
                    locIgnore.Add(dir);
                }

                // Generate a task queue for updating
                var queue = new List<UpdateItemInfo>();

                // Get folders added
                var locAdded = C3<string, string, string>.Except(
                    locScanned, locExist,
                    StringUtils.UniquePath, StringUtils.UniquePath,
                    new C1<string>.DefaultEqualityComparer()).ToList();

                foreach (string loc in locAdded)
                {
                    queue.Add(new UpdateItemInfo
                    {
                        Location = loc,
                        ItemType = locScannedDict[loc],
                        IsExist = false,
                    });
                }

                if (!skipExistingLocation)
                {
                    var locKept = C3<string, string, string>.Intersect(
                        locScanned, locExist,
                        StringUtils.UniquePath, StringUtils.UniquePath,
                        new C1<string>.DefaultEqualityComparer()).ToList();

                    foreach (string loc in locKept)
                    {
                        queue.Add(new UpdateItemInfo
                        {
                            Location = loc,
                            ItemType = locScannedDict[loc],
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
                }, "Update comic");

                if (watch.LapSpan().TotalSeconds > 2)
                {
                    DispatchComicUpdateEvent();
                    watch.Lap();
                }
            }
        }

        if (appSettings.RemoveUnreachableComics)
        {
            // Get removed folders
            var locRemoved = C3<string, string, string>.Except(locExist, locInLib,
                StringUtils.UniquePath, StringUtils.UniquePath,
                new C1<string>.DefaultEqualityComparer()).ToList();

            await TransactionBlock(delegate
            {
                // Remove folders from database
                foreach (string loc in locRemoved)
                {
                    // Skip directories in ignoring list
                    bool ignore = false;
                    foreach (string base_loc in locIgnore)
                    {
                        if (StringUtils.FolderContain(base_loc, loc))
                        {
                            ignore = true;
                            break;
                        }
                    }
                    if (ignore)
                    {
                        continue;
                    }

                    // Remove.
                    Log("Removing item '" + loc + "'");
                    RemoveWithLocationNoLock(loc);
                }
                return Task.CompletedTask;
            }, "RemoveLocationsFromDatabase");
        }

        return TaskException.Success;
    }

    private RequestOption CreateRequestOption()
    {
        return new(!IsExternal);
    }

    //
    // Utilities
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
