// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Database.SqlHelpers;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Tables;

namespace ComicReaderUWP.Data.Models.TagInfo;

internal class TagCategoryInfoModel
{
    private const string TAG = nameof(TagCategoryInfoModel);

    //
    // Member variables
    //

    private readonly ConcurrentDictionary<string, string> _ext = [];

    //
    // Properties
    //

    public string Name { get; private set; }

    private string ValueExt => JsonSerializer.Serialize(_ext);

    private TagCategoryInfoModel(string name)
    {
        Name = name;
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
        await TagInfoDatabase.Enqueue(() =>
        {
            SaveNoLock(this);
            return true;
        });
    }

    //
    // Static variables
    //

    private static readonly ConcurrentWeakPool<string, TagCategoryInfoModel> _cache = new();

    //
    // Static methods
    //

    public static void InjectPrivateMembers()
    {
        TagInfoDatabase.TagCategoryInfoGetOrCreateNoLock = GetOrCreateNoLock;
    }

    public static async Task<TagCategoryInfoModel?> Get(string name)
    {
        if (_cache.TryGetValue(name, out TagCategoryInfoModel? model))
        {
            return model;
        }

        return await TagInfoDatabase.Enqueue(() =>
        {
            if (_cache.TryGetValue(name, out TagCategoryInfoModel? model))
            {
                return model;
            }

            model = QueryNoLock(name);
            if (model is not null)
            {
                _cache.Set(name, model);
                return model;
            }

            return null;
        });
    }

    public static async Task<TagCategoryInfoModel> GetOrCreate(string name)
    {
        if (_cache.TryGetValue(name, out TagCategoryInfoModel? model))
        {
            return model;
        }

        return await TagInfoDatabase.Enqueue(() =>
        {
            return GetOrCreateNoLock(name);
        });
    }

    private static TagCategoryInfoModel GetOrCreateNoLock(string name)
    {
        if (_cache.TryGetValue(name, out TagCategoryInfoModel? model))
        {
            return model;
        }

        model = QueryNoLock(name);
        if (model is null)
        {
            model = new(name);
            SaveNoLock(model);
        }

        _cache.Set(name, model);
        return model;
    }

    private static TagCategoryInfoModel? QueryNoLock(string name)
    {
        SelectCommand command = SelectCommand.Create(TagCategoryInfoTable.Instance)
            .AppendCondition(TagCategoryInfoTable.ColumnName, name)
            .Limit(1);
        IReaderToken<string> extToken = command.PutQueryString(TagCategoryInfoTable.ColumnExt);
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

        TagCategoryInfoModel model = new(name);

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
                    model._ext[pair.Key] = pair.Value;
                }
            }
        }

        return model;
    }

    private static void SaveNoLock(TagCategoryInfoModel model)
    {
        string valueExt = model.ValueExt;

        int rowsUpdated = UpdateCommand.Create(TagCategoryInfoTable.Instance)
            .AppendCondition(TagCategoryInfoTable.ColumnName, model.Name)
            .AppendColumn(TagCategoryInfoTable.ColumnExt, valueExt)
            .Execute();

        if (rowsUpdated == 0)
        {
            InsertCommand.Create(TagCategoryInfoTable.Instance)
                .AppendColumn(TagCategoryInfoTable.ColumnName, model.Name)
                .AppendColumn(TagCategoryInfoTable.ColumnExt, valueExt)
                .Execute();
        }
    }

    private static void DeleteNoLock(string tagCategory)
    {
        TagInfoDatabase.TagInfoDeleteTagCategoryCacheOnlyNoLock(tagCategory);
        _cache.TryRemove(tagCategory, out _);

        DeleteCommand.Create(TagCategoryInfoTable.Instance)
            .AppendCondition(TagCategoryInfoTable.ColumnName, tagCategory)
            .Execute();
    }

    //
    // Utilities
    //

    public static async Task Rename(string oldName, string newName)
    {
        await TagInfoDatabase.Enqueue(() =>
        {
            DeleteNoLock(newName);

            TagInfoDatabase.TagInfoRenameTagCategoryCacheOnlyNoLock(oldName, newName);

            if (_cache.TryRemove(oldName, out TagCategoryInfoModel? model))
            {
                model.Name = newName;
                _cache.Set(newName, model);
            }

            UpdateCommand.Create(TagCategoryInfoTable.Instance)
                .AppendColumn(TagCategoryInfoTable.ColumnName, newName)
                .AppendCondition(TagCategoryInfoTable.ColumnName, oldName)
                .Execute();

            return true;
        });

        List<long> comicIds = [];
        await ComicHandle.Enqueue(() =>
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

        List<Task> tasks = [];
        List<ComicModel> comics = await ComicModel.BatchFromId(comicIds);
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

            tasks.Add(comic.SetTags(tags));
        }

        await Task.WhenAll(tasks);
        TagInfoDatabase.DispatchTagInfoUpdateEvents();
    }

    public static async Task Delete(string name)
    {
        await TagInfoDatabase.Enqueue(() =>
        {
            DeleteNoLock(name);
            return true;
        });

        HashSet<long> comicIds = [];
        await ComicHandle.Enqueue(() =>
        {
            SelectCommand command = SelectCommand.Create(TagCategoryTable.Instance)
                .AppendCondition(TagCategoryTable.ColumnName, name);
            IReaderToken<long> comicIdToken = command.PutQueryInt64(TagCategoryTable.ColumnComicId);
            using SelectCommand.IReader reader = command.Execute();
            while (reader.Read())
            {
                long comicId = comicIdToken.GetValue();
                comicIds.Add(comicId);
            }

            return true;
        });

        List<Task> tasks = [];
        List<ComicModel> comics = await ComicModel.BatchFromId(comicIds);
        foreach (ComicModel comic in comics)
        {
            Dictionary<string, HashSet<string>> comicTags = comic.TagsCopy;
            if (comicTags.Remove(name))
            {
                tasks.Add(comic.SetTags(comicTags));
            }
        }

        await Task.WhenAll(tasks);
        TagInfoDatabase.DispatchTagInfoUpdateEvents();
    }
}