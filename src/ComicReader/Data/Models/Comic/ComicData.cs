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
using ComicReader.Common.Utils;
using ComicReader.Data.Tables;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Lifecycle;
using ComicReader.SDK.Common.Threading;
using ComicReader.SDK.Common.Utils;
using ComicReader.SDK.Database.SqlHelpers;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Data.Models.Comic;

internal abstract class ComicData
{
    //
    // Constants
    //

    private const string TAG = nameof(ComicData);
    private const int COVER_INDEX = 0;

    //
    // Static Variables
    //

    private static readonly MutableLiveData<bool> _isScanningLibrary = new(false);
    public static LiveData<bool> IsScanningLibrary => _isScanningLibrary;

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
                ComicCompletionStatusEnum completionState = ParseCompletionState(completionStateToken.GetValue());
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

    private static ComicCompletionStatusEnum ParseCompletionState(int value)
    {
        if (Enum.IsDefined(typeof(ComicCompletionStatusEnum), value))
        {
            return (ComicCompletionStatusEnum)value;
        }

        return ComicCompletionStatusEnum.NotStarted;
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

    public async Task FlushExt()
    {
        await Enqueue("FlushExt", () =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnExt, GetColumnValue(ComicTable.ColumnExt))
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
            return true;
        });
    }

    public async Task SetTitle1(string title)
    {
        Title1 = title;
        await Enqueue("SetTitle1", () =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnTitle1, GetColumnValue(ComicTable.ColumnTitle1))
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
            return true;
        });
    }

    public async Task SetTitle2(string title)
    {
        Title2 = title;
        await Enqueue("SetTitle2", () =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnTitle2, GetColumnValue(ComicTable.ColumnTitle2))
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
            return true;
        });
    }

    public async Task SetDescription(string description)
    {
        Description = description;
        await Enqueue("SetDescription", () =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnDescription, GetColumnValue(ComicTable.ColumnDescription))
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
            return true;
        });
    }

    public async Task SetTags(IReadOnlyDictionary<string, HashSet<string>> tags)
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
        await Enqueue("SetTags", () =>
        {
            SaveNoLock(() =>
            {
                InternalSaveTagsNoLock();
            });
            return true;
        });
    }

    public async Task SetLocation(string location)
    {
        Location = location;
        await Enqueue("SetLocation", () =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnLocation, GetColumnValue(ComicTable.ColumnLocation))
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
            return true;
        });
    }

    public async Task SetRating(int rating)
    {
        rating = Math.Clamp(rating, -1, 100);
        Rating = rating;
        await Enqueue("SaveRating", delegate
        {
            SaveNoLock(delegate
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnRating, GetColumnValue(ComicTable.ColumnRating))
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
            return true;
        });
    }

    private async Task SetPageCount(int pageCount)
    {
        if (PageCount == pageCount)
        {
            return;
        }

        PageCount = pageCount;
        await Enqueue("SetPageCount", () =>
        {
            SaveNoLock(() =>
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnPageCount, GetColumnValue(ComicTable.ColumnPageCount))
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
                await SetPageCount(pageCount);
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
            ComicTable.ColumnCoverCacheKey,
            ComicTable.ColumnDescription,
            ComicTable.ColumnCompletionState,
            ComicTable.ColumnExt,
            ComicTable.ColumnPageCount,
        ];
    });

    private static readonly Lazy<IReadOnlyDictionary<string, Func<ComicData, object>>> _columnValueEvaluator = new(() =>
    {
        Dictionary<string, Func<ComicData, object>> evaluators = [];
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
        evaluators[ComicTable.ColumnCoverCacheKey.Name] = i => TypeAssert.AssertString(i.CoverCacheKey);
        evaluators[ComicTable.ColumnDescription.Name] = i => TypeAssert.AssertString(i.Description);
        evaluators[ComicTable.ColumnCompletionState.Name] = i => TypeAssert.AssertInt((int)i.CompletionState);
        evaluators[ComicTable.ColumnExt.Name] = i => TypeAssert.AssertString(JsonSerializer.Serialize(i._ext));
        evaluators[ComicTable.ColumnPageCount.Name] = i => TypeAssert.AssertInt(i.PageCount);
        return evaluators;
    });

    private object GetColumnValue(IColumnTypeless column)
    {
        Func<ComicData, object> evaluator = _columnValueEvaluator.Value[column.Name];
        return evaluator(this);
    }

    //
    // Unsorted
    //

    private void SaveAllNoLock()
    {
        SaveNoLock(delegate
        {
            UpdateCommand command = UpdateCommand.Create(ComicTable.Instance)
                .AppendCondition(ComicTable.ColumnId, Id);
            foreach (IColumnTypeless column in _allNonIdColumns.Value)
            {
                command.AppendColumn(column, GetColumnValue(column));
            }

            command.Execute();
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
                    .AppendColumn(ComicTable.ColumnHidden, GetColumnValue(ComicTable.ColumnHidden))
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
            return true;
        });
    }

    public async Task SaveCompletionState(ComicCompletionStatusEnum completionState)
    {
        CompletionState = completionState;

        await Enqueue("SaveCompletionState", delegate
        {
            SaveNoLock(delegate
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnCompletionState, GetColumnValue(ComicTable.ColumnCompletionState))
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
            return true;
        });
    }

    public async Task SaveProgressAsync(int progress, double last_position)
    {
        Progress = Math.Clamp(progress, -1, 100);
        LastPosition = last_position;

        await Enqueue("SaveProgress", delegate
        {
            SaveNoLock(delegate
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnProgress, GetColumnValue(ComicTable.ColumnProgress))
                    .AppendColumn(ComicTable.ColumnLastPosition, GetColumnValue(ComicTable.ColumnLastPosition))
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

        CoroutineUtils.Start(() => Enqueue("SetAsRead", delegate
        {
            SaveNoLock(delegate
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnProgress, GetColumnValue(ComicTable.ColumnProgress))
                    .AppendColumn(ComicTable.ColumnLastVisit, GetColumnValue(ComicTable.ColumnLastVisit))
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
            return true;
        }));
    }

    public void SetCoverCacheKey(string key)
    {
        CoverCacheKey = key;

        CoroutineUtils.Start(() => Enqueue("SetCoverCacheKey", delegate
        {
            SaveNoLock(delegate
            {
                UpdateCommand.Create(ComicTable.Instance)
                    .AppendColumn(ComicTable.ColumnCoverCacheKey, GetColumnValue(ComicTable.ColumnCoverCacheKey))
                    .AppendCondition(ComicTable.ColumnId, Id)
                    .Execute();
            });
            return true;
        }));
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

        Title1 = tags[^1];

        TagData defaultTag = new(StringResourceProvider.Instance.Default, tags.Skip(1).ToHashSet());
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

    public static void UpdateAllComics(string reason)
    {
        int pendingCount = Interlocked.Increment(ref _pendingUpdateTaskCount);
        Logger.I(TAG, $"UpdateAllComics(reason={reason})");
        TaskDispatcher.LongRunningThreadPool.Submit("UpdateAllComics", delegate
        {
            int pendingCount = Interlocked.Decrement(ref _pendingUpdateTaskCount);
            if (pendingCount > 0)
            {
                // Only keep the last request
                return;
            }

            _isScanningLibrary.Emit(true);
            try
            {
                UpdateAllComicsInternal();
            }
            finally
            {
                _isScanningLibrary.Emit(false);
            }
        });
    }

    public abstract string GetImageCacheKey(int index);

    public abstract int GetImageSignature(int index);

    protected abstract Task<bool> ReloadImages();

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
        var command = InsertCommand.Create(ComicTable.Instance);
        foreach (IColumnTypeless column in _allNonIdColumns.Value)
        {
            command.AppendColumn(column, GetColumnValue(column));
        }

        Id = command.Execute();
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

    private static void UpdateAllComicsInternal()
    {
        AppSettingsModel.ExternalModel appSettings = AppSettingsModel.Instance.GetModel();
        bool comicUpdatedSinceLastBroadcast = false;

        // Fetch all locations in the database
        var oldLocations = new List<string>();
        Enqueue("GetLocationsFromDatabase", delegate
        {
            var command = SelectCommand.Create(ComicTable.Instance);
            IReaderToken<string> locationToken = command.PutQueryString(ComicTable.ColumnLocation);
            using SelectCommand.IReader reader = command.Execute();
            while (reader.Read())
            {
                oldLocations.Add(locationToken.GetValue());
            }

            return true;
        }).Wait();

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
            while (ctx.Search(1024).Result)
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

                // Create/Update comics
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
                    });
                }

                if (queue.Count > 0)
                {
                    comicUpdatedSinceLastBroadcast = true;
                    TransactionBlock(async delegate
                    {
                        foreach (UpdateItemInfo info in queue)
                        {
                            ComicData? comic = FromDatabase(info.ItemType, info.Location);
                            if (comic is null)
                            {
                                continue;
                            }

                            comic.SetAsDefaultInfo();
                            comic.SaveAllNoLock();
                        }

                        await Task.CompletedTask;
                    }, "UpdateComic").Wait();
                }

                if (watch.LapSpan().TotalSeconds > 2 && comicUpdatedSinceLastBroadcast)
                {
                    comicUpdatedSinceLastBroadcast = false;
                    DispatchComicUpdateEvent();
                    watch.Lap();
                }
            }
        }

        // Remove comics
        List<string> locationRemoved = [];
        if (appSettings.RemoveUnreachableComics)
        {
            locationRemoved = [.. C3<string, string, string>.Except(
                oldLocations, newLocations,
                StringUtils.UniquePath, StringUtils.UniquePath,
                new C1<string>.DefaultEqualityComparer())];
        }

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
                DialogUtils.DialogOptions options = new DialogUtils.DialogOptions.Builder()
                    .SetTitle(StringResourceProvider.Instance.Warning)
                    .SetContent(promptContent)
                    .SetPrimaryButtonText(StringResourceProvider.Instance.Remove)
                    .SetCloseButtonText(StringResourceProvider.Instance.Cancel)
                    .Build();
                proceed = DialogUtils.EnqueueDialogAsync(options).Result == ContentDialogResult.Primary;
            }

            if (proceed)
            {
                comicUpdatedSinceLastBroadcast = true;
                TransactionBlock(delegate
                {
                    foreach (string location in locationRemoved)
                    {
                        Logger.I(TAG, $"Removing: {location}");
                        RemoveWithLocationNoLock(location);
                    }

                    return Task.CompletedTask;
                }, "RemoveLocationsFromDatabase").Wait();
            }
        }

        if (comicUpdatedSinceLastBroadcast)
        {
            DispatchComicUpdateEvent();
        }
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
