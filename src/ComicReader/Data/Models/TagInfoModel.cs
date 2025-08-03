// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Utils;
using ComicReader.Data.Models.Comic;
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

    public string TagCategory { get; private set; }
    public string Tag { get; private set; }

    private string ValueExt => JsonSerializer.Serialize(_ext);

    private TagInfoModel(string tagCategory, string tag)
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

        DispatchTagInfoUpdateEvents();
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

    public static async Task<T> Enqueue<T>(string taskName, Func<T> op)
    {
        var taskResult = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _databaseDispatcher.Submit(taskName, delegate
        {
            taskResult.SetResult(op());
        });

        return await taskResult.Task;
    }

    public static async Task<TagInfoModel?> Get(string tagCategory, string tag)
    {
        Key key = new(tagCategory, tag);
        if (_cache.TryGetValue(key, out TagInfoModel? model))
        {
            return model;
        }

        return await Enqueue("Get", () =>
        {
            TagInfoModel? model = QueryNoLock(tagCategory, tag);
            if (model == null)
            {
                return null;
            }

            return _cache.GetOrAdd(key, model);
        });
    }

    public static async Task<TagInfoModel> GetOrCreate(string tagCategory, string tag)
    {
        Key key = new(tagCategory, tag);
        if (_cache.TryGetValue(key, out TagInfoModel? model))
        {
            return model;
        }

        return await Enqueue("GetOrCreate", () =>
        {
            TagInfoModel? model = QueryNoLock(tagCategory, tag);
            if (model != null)
            {
                return _cache.GetOrAdd(key, model);
            }

            model = new(tagCategory, tag);
            SaveNoLock(model);
            return _cache.GetOrAdd(key, model);
        });
    }

    private static TagInfoModel? QueryNoLock(string tagCategory, string tag)
    {
        SelectCommand command = SelectCommand.Create(TagInfoTable.Instance)
            .AppendCondition(TagInfoTable.ColumnTagCategory, tagCategory)
            .AppendCondition(TagInfoTable.ColumnTag, tag)
            .Limit(1);
        IReaderToken<string> extToken = command.PutQueryString(TagInfoTable.ColumnExt);
        using SelectCommand.IReader reader = command.Execute();

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

        TagInfoModel model = new(tagCategory, tag);

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

        int rowsUpdated = UpdateCommand.Create(TagInfoTable.Instance)
            .AppendCondition(TagInfoTable.ColumnTagCategory, model.TagCategory)
            .AppendCondition(TagInfoTable.ColumnTag, model.Tag)
            .AppendColumn(TagInfoTable.ColumnExt, valueExt)
            .Execute();

        if (rowsUpdated == 0)
        {
            InsertCommand.Create(TagInfoTable.Instance)
                .AppendColumn(TagInfoTable.ColumnTagCategory, model.TagCategory)
                .AppendColumn(TagInfoTable.ColumnTag, model.Tag)
                .AppendColumn(TagInfoTable.ColumnExt, valueExt)
                .Execute();
        }
    }

    private static void DeleteTagCategoryNoLock(string tagCategory)
    {
        List<string> tags = [];
        SelectCommand command = SelectCommand.Create(TagInfoTable.Instance)
            .AppendCondition(TagInfoTable.ColumnTagCategory, tagCategory);
        IReaderToken<string> tagToken = command.PutQueryString(TagInfoTable.ColumnTag);
        using SelectCommand.IReader reader = command.Execute();
        while (reader.Read())
        {
            string tag = tagToken.GetValue();
            tags.Add(tag);
        }

        DeleteCommand.Create(TagInfoTable.Instance)
            .AppendCondition(TagInfoTable.ColumnTagCategory, tagCategory)
            .Execute();

        foreach (string tag in tags)
        {
            Key key = new(tagCategory, tag);
            _cache.TryRemove(key, out _);
        }
    }

    private static void DeleteTagNoLock(string tagCategory, string tag)
    {
        DeleteCommand.Create(TagInfoTable.Instance)
            .AppendCondition(TagInfoTable.ColumnTagCategory, tagCategory)
            .AppendCondition(TagInfoTable.ColumnTag, tag)
            .Execute();
        Key key = new(tagCategory, tag);
        _cache.TryRemove(key, out _);
    }

    private static void DispatchTagInfoUpdateEvents()
    {
        GlobalEvent.Instance.TagInfoUpdated.Emit(0);
    }

    //
    // Utilities
    //

    public static async Task DeleteTag(string tagCategory, string tag)
    {
        await Enqueue("DeleteTag", () =>
        {
            DeleteTagNoLock(tagCategory, tag);
            return true;
        });

        HashSet<long> comicIds = [];
        await ComicData.Enqueue("DeleteTag", () =>
        {
            SelectCommand subQuery = SelectCommand.Create(TagCategoryTable.Instance)
                .AppendCondition(TagCategoryTable.ColumnName, tagCategory);
            subQuery.PutQueryInt64(TagCategoryTable.ColumnId);
            SelectCommand command = SelectCommand.Create(TagTable.Instance)
                .AppendCondition(TagTable.ColumnContent, tag)
                .AppendCondition(new InCondition(ColumnOrValue.FromColumn(TagTable.ColumnTagCategoryId), subQuery));
            IReaderToken<long> comicIdToken = command.PutQueryInt64(TagTable.ColumnComicId);
            using SelectCommand.IReader reader = command.Execute();
            while (reader.Read())
            {
                long comicId = comicIdToken.GetValue();
                comicIds.Add(comicId);
            }

            return true;
        });

        List<ComicModel> comics = await ComicModel.BatchFromId("DeleteTag", comicIds);
        foreach (ComicModel comic in comics)
        {
            Dictionary<string, HashSet<string>> comicTags = comic.TagsCopy;
            if (comicTags.TryGetValue(tagCategory, out HashSet<string>? tags))
            {
                tags.Remove(tag);
                comic.SetTags(comicTags);
            }
        }

        DispatchTagInfoUpdateEvents();
    }

    public static async Task DeleteTagCategory(string tagCategory)
    {
        await Enqueue("DeleteTag", () =>
        {
            DeleteCommand.Create(TagInfoTable.Instance)
                .AppendCondition(TagInfoTable.ColumnTagCategory, tagCategory)
                .Execute();
            return true;
        });

        HashSet<long> comicIds = [];
        await ComicData.Enqueue("DeleteTag", () =>
        {
            SelectCommand command = SelectCommand.Create(TagCategoryTable.Instance)
                .AppendCondition(TagCategoryTable.ColumnName, tagCategory);
            IReaderToken<long> comicIdToken = command.PutQueryInt64(TagCategoryTable.ColumnComicId);
            using SelectCommand.IReader reader = command.Execute();
            while (reader.Read())
            {
                long comicId = comicIdToken.GetValue();
                comicIds.Add(comicId);
            }

            return true;
        });

        List<ComicModel> comics = await ComicModel.BatchFromId("DeleteTag", comicIds);
        foreach (ComicModel comic in comics)
        {
            Dictionary<string, HashSet<string>> comicTags = comic.TagsCopy;
            if (comicTags.Remove(tagCategory))
            {
                comic.SetTags(comicTags);
            }
        }

        DispatchTagInfoUpdateEvents();
    }

    public static async Task RenameTagCategory(string oldName, string newName)
    {
        await Enqueue("RenameTagCategory", () =>
        {
            DeleteTagCategoryNoLock(newName);

            List<string> tags = [];
            SelectCommand command = SelectCommand.Create(TagInfoTable.Instance)
                .AppendCondition(TagInfoTable.ColumnTagCategory, oldName);
            IReaderToken<string> tagToken = command.PutQueryString(TagInfoTable.ColumnTag);
            using SelectCommand.IReader reader = command.Execute();
            while (reader.Read())
            {
                string tag = tagToken.GetValue();
                tags.Add(tag);
            }

            UpdateCommand.Create(TagInfoTable.Instance)
                .AppendColumn(TagInfoTable.ColumnTagCategory, newName)
                .AppendCondition(TagInfoTable.ColumnTagCategory, oldName)
                .Execute();

            foreach (string tag in tags)
            {
                Key oldKey = new(oldName, tag);
                Key newKey = new(newName, tag);
                if (_cache.TryRemove(oldKey, out TagInfoModel? tagInfoModel))
                {
                    tagInfoModel.TagCategory = newName;
                    _cache.Set(newKey, tagInfoModel);
                }
            }

            return true;
        });

        List<long> comicIds = [];
        await ComicData.Enqueue("RenameTagCategory", () =>
        {
            SelectCommand command = SelectCommand.Create(TagCategoryTable.Instance)
                .AppendCondition(TagCategoryTable.ColumnName, oldName);
            IReaderToken<long> comicIdToken = command.PutQueryInt64(TagCategoryTable.ColumnComicId);
            using SelectCommand.IReader reader = command.Execute();
            while (reader.Read())
            {
                long comicId = comicIdToken.GetValue();
                comicIds.Add(comicId);
            }

            return true;
        });

        List<ComicModel> comics = await ComicModel.BatchFromId("RenameTagCategory", comicIds);
        foreach (ComicModel comic in comics)
        {
            Dictionary<string, HashSet<string>> tags = comic.TagsCopy;
            if (!tags.TryGetValue(newName, out HashSet<string>? tagSet))
            {
                tagSet = [];
                tags[newName] = tagSet;
            }

            if (tags.TryGetValue(oldName, out HashSet<string>? oldTagSet))
            {
                tags.Remove(oldName);
                foreach (string tag in oldTagSet)
                {
                    tagSet.Add(tag);
                }
            }

            comic.SetTags(tags);
        }

        DispatchTagInfoUpdateEvents();
    }

    public static async Task RenameTag(string oldTagCategory, string oldTag, string newTagCategory, string newTag)
    {
        await Enqueue("RenameTag", () =>
        {
            DeleteTagNoLock(newTagCategory, newTag);

            if (_cache.TryRemove(new Key(oldTagCategory, oldTag), out TagInfoModel? tagInfoModel))
            {
                tagInfoModel.TagCategory = newTagCategory;
                tagInfoModel.Tag = newTag;
                _cache.Set(new Key(newTagCategory, newTag), tagInfoModel);
            }

            UpdateCommand.Create(TagInfoTable.Instance)
                .AppendColumn(TagInfoTable.ColumnTagCategory, newTagCategory)
                .AppendColumn(TagInfoTable.ColumnTag, newTag)
                .AppendCondition(TagInfoTable.ColumnTagCategory, oldTagCategory)
                .AppendCondition(TagInfoTable.ColumnTag, oldTag)
                .Execute();
            return true;
        });

        List<long> comicIds = [];
        await ComicData.Enqueue("RenameTag", () =>
        {
            SelectCommand subQuery = SelectCommand.Create(TagCategoryTable.Instance)
                .AppendCondition(TagCategoryTable.ColumnName, oldTagCategory);
            subQuery.PutQueryInt64(TagCategoryTable.ColumnId);
            SelectCommand command = SelectCommand.Create(TagTable.Instance)
                .AppendCondition(TagTable.ColumnContent, oldTag)
                .AppendCondition(new InCondition(ColumnOrValue.FromColumn(TagTable.ColumnTagCategoryId), subQuery));
            IReaderToken<long> comicIdToken = command.PutQueryInt64(TagTable.ColumnComicId);
            using SelectCommand.IReader reader = command.Execute();
            while (reader.Read())
            {
                long comicId = comicIdToken.GetValue();
                comicIds.Add(comicId);
            }

            return true;
        });

        List<ComicModel> comics = await ComicModel.BatchFromId("RenameTag", comicIds);
        foreach (ComicModel comic in comics)
        {
            Dictionary<string, HashSet<string>> comicTags = comic.TagsCopy;
            if (comicTags.TryGetValue(oldTagCategory, out HashSet<string>? tags))
            {
                tags.Remove(oldTag);
                if (!comicTags.TryGetValue(newTagCategory, out HashSet<string>? newTags))
                {
                    newTags = [];
                    comicTags[newTagCategory] = newTags;
                }

                newTags.Add(newTag);
                comic.SetTags(comicTags);
            }
        }

        DispatchTagInfoUpdateEvents();
    }

    //
    // Types
    //

    private class Key(string tagCategory, string tag)
    {
        public string TagCategory { get; } = tagCategory;
        public string Tag { get; } = tag;

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
