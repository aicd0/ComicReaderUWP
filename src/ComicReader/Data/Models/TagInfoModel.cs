// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

using ComicReader.Common.Utils;
using ComicReader.Data.Tables;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Threading;
using ComicReader.SDK.Data.SqlHelpers;

namespace ComicReader.Data.Models;

internal class TagInfoModel
{
    //
    // Member variables
    //

    private readonly ConcurrentDictionary<string, string> _ext = [];

    //
    // Properties
    //

    public string Tag { get; private set; }
    public string TagCategory { get; private set; }

    private string ValueExt => JsonSerializer.Serialize(_ext);

    private TagInfoModel(string tag, string tagCategory)
    {
        Tag = tag;
        TagCategory = tagCategory;
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
            SaveNoLock(this);
            return true;
        });
    }

    //
    // Static variables
    //

    private static readonly ITaskDispatcher _databaseDispatcher = TaskDispatcher.Factory.NewQueue("TagInfoDatabaseQueue");
    private static readonly ConcurrentWeakPool<Key, TagInfoModel> _cache = new();

    //
    // Static methods
    //

    public static async Task<TagInfoModel> Get(string tag, string tagCategory)
    {
        Key key = new(tag, tagCategory);
        if (_cache.TryGetValue(key, out TagInfoModel? model))
        {
            return model;
        }

        model = await Enqueue("Get", () =>
        {
            TagInfoModel? model = QueryNoLock(tag, tagCategory);
            if (model != null)
            {
                return model;
            }

            model = new(tag, tagCategory);
            SaveNoLock(model);
            return model;
        });

        return _cache.GetOrAdd(key, model);
    }

    private static TagInfoModel? QueryNoLock(string tag, string tagCategory)
    {
        SelectCommand command = SelectCommand.Create(TagInfoTable.Instance)
            .AppendCondition(TagInfoTable.ColumnTag, tag)
            .AppendCondition(TagInfoTable.ColumnTagCategory, tagCategory)
            .Limit(1);
        IReaderToken<string> extToken = command.PutQueryString(TagInfoTable.ColumnExt);
        SelectCommand.IReader reader = command.Execute(SqlDatabaseManager.TagInfoDatabase);

        bool hasRecord = false;
        string extJson = string.Empty;
        while (reader.Read())
        {
            hasRecord = true;
            extJson = extToken.GetValue();
        }

        if (!hasRecord)
        {
            return null;
        }

        TagInfoModel model = new(tag, tagCategory);

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
                    model._ext[pair.Key] = pair.Value;
                }
            }
        }

        return model;
    }

    private static void SaveNoLock(TagInfoModel model)
    {
        string valueExt = model.ValueExt;

        InsertCommand.Create(TagInfoTable.Instance)
            .AppendColumn(TagInfoTable.ColumnTag, model.Tag)
            .AppendColumn(TagInfoTable.ColumnTagCategory, model.TagCategory)
            .AppendColumn(TagInfoTable.ColumnExt, valueExt)
            .OnConflict([TagInfoTable.ColumnTag, TagInfoTable.ColumnTagCategory])
            .Update(TagInfoTable.ColumnExt, valueExt)
            .End()
            .Execute(SqlDatabaseManager.TagInfoDatabase);
    }

    private static async Task<T> Enqueue<T>(string taskName, Func<T> op)
    {
        var taskResult = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _databaseDispatcher.Submit(taskName, delegate
        {
            taskResult.SetResult(op());
        });

        return await taskResult.Task;
    }

    //
    // Types
    //

    private class Key(string tag, string tagCategory)
    {
        public string Tag { get; } = tag;
        public string TagCategory { get; } = tagCategory;

        public override bool Equals(object? obj)
        {
            if (obj is Key other)
            {
                return Tag == other.Tag && TagCategory == other.TagCategory;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Tag, TagCategory);
        }
    }
}
