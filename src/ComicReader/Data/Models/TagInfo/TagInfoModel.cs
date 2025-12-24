// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

using ComicReader.Common.Utils;
using ComicReader.Data.Models.Comic;
using ComicReader.Data.Tables;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Database.SqlHelpers;

namespace ComicReader.Data.Models.TagInfo;

internal class TagInfoModel
{
    private const string TAG = nameof(TagInfoModel);

    //
    // Member variables
    //

    private readonly ConcurrentDictionary<string, string> _ext = [];

    //
    // Properties
    //

    public string TagCategory { get; private set; }
    public string Name { get; private set; }

    private string ValueExt => JsonSerializer.Serialize(_ext);

    private TagInfoModel(string tagCategory, string tag)
    {
        Name = tag;
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

        TagInfoDatabase.DispatchTagInfoUpdateEvents();
    }

    public async Task FlushExt()
    {
        await TagInfoDatabase.Enqueue("FlushExt", () =>
        {
            SaveNoLock(this);
            return true;
        });
    }

    //
    // Static variables
    //

    private static readonly ConcurrentWeakPool<Key, TagInfoModel> _cache = new();

    //
    // Static methods
    //

    public static void InjectPrivateMembers()
    {
        TagInfoDatabase.TagInfoDeleteTagCategoryCacheOnlyNoLock = DeleteTagCategoryCacheOnlyNoLock;
        TagInfoDatabase.TagInfoRenameTagCategoryCacheOnlyNoLock = RenameTagCategoryCacheOnlyNoLock;
    }

    public static async Task<TagInfoModel?> Get(string tagCategory, string tag)
    {
        Key key = new(tagCategory, tag);
        if (_cache.TryGetValue(key, out TagInfoModel? model))
        {
            return model;
        }

        return await TagInfoDatabase.Enqueue("Get", () =>
        {
            if (_cache.TryGetValue(key, out TagInfoModel? model))
            {
                return model;
            }

            model = QueryNoLock(tagCategory, tag);
            if (model is not null)
            {
                _cache.Set(key, model);
                return model;
            }

            return null;
        });
    }

    public static async Task<TagInfoModel> GetOrCreate(string tagCategory, string tag)
    {
        Key key = new(tagCategory, tag);
        if (_cache.TryGetValue(key, out TagInfoModel? model))
        {
            return model;
        }

        return await TagInfoDatabase.Enqueue("GetOrCreate", () =>
        {
            if (_cache.TryGetValue(key, out TagInfoModel? model))
            {
                return model;
            }

            model = QueryNoLock(tagCategory, tag);
            if (model is null)
            {
                model = new(tagCategory, tag);
                _cache.Set(key, model);
                SaveNoLock(model);
            }
            else
            {
                _cache.Set(key, model);
            }

            return model;
        });
    }

    private static TagInfoModel? QueryNoLock(string tagCategory, string tag)
    {
        SelectCommand command = SelectCommand.Create(TagInfoTable.Instance)
            .AppendCondition(TagInfoTable.ColumnTagCategory, tagCategory)
            .AppendCondition(TagInfoTable.ColumnName, tag)
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
            catch (Exception e)
            {
                Logger.F(TAG, e);
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
            .AppendCondition(TagInfoTable.ColumnName, model.Name)
            .AppendColumn(TagInfoTable.ColumnExt, valueExt)
            .Execute();

        if (rowsUpdated == 0)
        {
            // Ensure tag category exists
            TagInfoDatabase.TagCategoryInfoGetOrCreateNoLock(model.TagCategory);

            InsertCommand.Create(TagInfoTable.Instance)
                .AppendColumn(TagInfoTable.ColumnTagCategory, model.TagCategory)
                .AppendColumn(TagInfoTable.ColumnName, model.Name)
                .AppendColumn(TagInfoTable.ColumnExt, valueExt)
                .Execute();
        }
    }

    private static void DeleteNoLock(string tagCategory, string tag)
    {
        Key key = new(tagCategory, tag);
        _cache.TryRemove(key, out _);

        DeleteCommand.Create(TagInfoTable.Instance)
            .AppendCondition(TagInfoTable.ColumnTagCategory, tagCategory)
            .AppendCondition(TagInfoTable.ColumnName, tag)
            .Execute();
    }

    private static void DeleteTagCategoryCacheOnlyNoLock(string tagCategory)
    {
        List<string> tags = [];
        SelectCommand command = SelectCommand.Create(TagInfoTable.Instance)
            .AppendCondition(TagInfoTable.ColumnTagCategory, tagCategory);
        IReaderToken<string> tagToken = command.PutQueryString(TagInfoTable.ColumnName);
        using SelectCommand.IReader reader = command.Execute();
        while (reader.Read())
        {
            string tag = tagToken.GetValue();
            tags.Add(tag);
        }

        foreach (string tag in tags)
        {
            Key key = new(tagCategory, tag);
            _cache.TryRemove(key, out _);
        }
    }

    private static void RenameTagCategoryCacheOnlyNoLock(string oldName, string newName)
    {
        List<string> tags = [];
        SelectCommand command = SelectCommand.Create(TagInfoTable.Instance)
            .AppendCondition(TagInfoTable.ColumnTagCategory, oldName);
        IReaderToken<string> tagToken = command.PutQueryString(TagInfoTable.ColumnName);
        using SelectCommand.IReader reader = command.Execute();
        while (reader.Read())
        {
            string tag = tagToken.GetValue();
            tags.Add(tag);
        }

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
    }

    //
    // Utilities
    //

    public static async Task Delete(string tagCategory, string tag)
    {
        await TagInfoDatabase.Enqueue("DeleteTag", () =>
        {
            DeleteNoLock(tagCategory, tag);
            return true;
        });

        HashSet<long> comicIds = [];
        await ComicHandle.Enqueue("DeleteTag", () =>
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

        List<Task> tasks = [];
        List<ComicModel> comics = await ComicModel.BatchFromId("DeleteTag", comicIds);
        foreach (ComicModel comic in comics)
        {
            Dictionary<string, HashSet<string>> comicTags = comic.TagsCopy;
            if (comicTags.TryGetValue(tagCategory, out HashSet<string>? tags))
            {
                tags.Remove(tag);
                tasks.Add(comic.SetTags(comicTags));
            }
        }

        await Task.WhenAll(tasks);
        TagInfoDatabase.DispatchTagInfoUpdateEvents();
    }

    public static async Task Rename(string oldTagCategory, string oldTag, string newTagCategory, string newTag)
    {
        await TagInfoDatabase.Enqueue("RenameTag", () =>
        {
            DeleteNoLock(newTagCategory, newTag);

            if (_cache.TryRemove(new Key(oldTagCategory, oldTag), out TagInfoModel? tagInfoModel))
            {
                tagInfoModel.TagCategory = newTagCategory;
                tagInfoModel.Name = newTag;
                _cache.Set(new Key(newTagCategory, newTag), tagInfoModel);
            }

            TagInfoDatabase.TagCategoryInfoGetOrCreateNoLock(newTagCategory);
            UpdateCommand.Create(TagInfoTable.Instance)
                .AppendColumn(TagInfoTable.ColumnTagCategory, newTagCategory)
                .AppendColumn(TagInfoTable.ColumnName, newTag)
                .AppendCondition(TagInfoTable.ColumnTagCategory, oldTagCategory)
                .AppendCondition(TagInfoTable.ColumnName, oldTag)
                .Execute();
            return true;
        });

        List<long> comicIds = [];
        await ComicHandle.Enqueue("RenameTag", () =>
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

        List<Task> tasks = [];
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
                tasks.Add(comic.SetTags(comicTags));
            }
        }

        await Task.WhenAll(tasks);
        TagInfoDatabase.DispatchTagInfoUpdateEvents();
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
