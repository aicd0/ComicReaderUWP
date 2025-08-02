// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using ComicReader.Common;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Data;

namespace ComicReader.Data.Models;

internal class HistoryModel : JsonDatabase<HistoryModel.JsonModel>
{
    public class JsonModel
    {
        [JsonPropertyName("Items")]
        public List<JsonItemModel?>? Items { get; set; }
    }

    public class JsonItemModel
    {
        [JsonPropertyName("Id")]
        public long? Id { get; set; }

        [JsonPropertyName("Title")]
        public string? Title { get; set; }

        [JsonPropertyName("DateTime")]
        public long? DateTime { get; set; }
    }

    public static readonly HistoryModel Instance = new();

    private HistoryModel() : base("history.json") { }

    protected override JsonModel CreateModel()
    {
        return new();
    }

    public ExternalModel GetModel()
    {
        return Read(ConvertToExternalModel);
    }

    public void UpdateModel(ExternalModel model)
    {
        Write(m =>
        {
            m.Items ??= [];
            m.Items.Clear();
            HashSet<long> comicIds = [];
            foreach (ExternalItemModel item in model.Items)
            {
                if (!comicIds.Add(item.Id))
                {
                    continue;
                }
                m.Items.Add(new JsonItemModel
                {
                    Id = item.Id,
                    Title = item.Title,
                    DateTime = item.DateTime.ToUnixTimeMilliseconds(),
                });
            }
            return true;
        });
        Save();
        DispatchUpdateEvent();
    }

    public void Add(long id, string title, bool sendEvent)
    {
        bool updated = Write(delegate (JsonModel model)
        {
            model.Items ??= [];
            model.Items.RemoveAll(item => item is null || item.Id == id);
            JsonItemModel newItem = new()
            {
                Id = id,
                Title = title,
                DateTime = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
            };
            model.Items.Add(newItem);
            return true;
        });

        if (updated)
        {
            Save();
            if (sendEvent)
            {
                DispatchUpdateEvent();
            }
        }
    }

    public void Remove(long id, bool sendEvent)
    {
        bool updated = Write(delegate (JsonModel model)
        {
            model.Items ??= [];
            return model.Items.RemoveAll(item => item is null || item.Id == id) > 0;
        });

        if (updated)
        {
            Save();
            if (sendEvent)
            {
                DispatchUpdateEvent();
            }
        }
    }

    public void Clear(bool sendEvent)
    {
        bool updated = Write(delegate (JsonModel model)
        {
            model.Items?.Clear();
            return true;
        });
        if (updated)
        {
            Save();
            if (sendEvent)
            {
                DispatchUpdateEvent();
            }
        }
    }

    private void DispatchUpdateEvent()
    {
        GlobalEvent.Instance.HistoryUpdated.Emit(0);
    }

    private static ExternalModel ConvertToExternalModel(JsonModel model)
    {
        if (model == null)
        {
            return new([]);
        }

        var items = new List<ExternalItemModel>();
        foreach (JsonItemModel? child in model.Items ?? [])
        {
            if (child is null)
            {
                continue;
            }
            ExternalItemModel? itemModel = ConvertToExternalModel(child);
            if (itemModel is null)
            {
                continue;
            }
            items.Add(itemModel);
        }
        return new ExternalModel(items);
    }

    private static ExternalItemModel? ConvertToExternalModel(JsonItemModel item)
    {
        long id = item.Id ?? -1;
        if (id < 0)
        {
            return null;
        }
        string title = item.Title ?? string.Empty;
        DateTimeOffset dateTime;
        try
        {
            dateTime = DateTimeOffset.FromUnixTimeMilliseconds(item.DateTime ?? 0);
        }
        catch (ArgumentOutOfRangeException e)
        {
            Logger.AssertNotReachHere("D8C51D0C8DD2EABE", e);
            dateTime = DateTimeOffset.MinValue;
        }
        return new ExternalItemModel(id, title, dateTime);
    }

    public class ExternalModel(List<ExternalItemModel> items)
    {
        public List<ExternalItemModel> Items { get; set; } = items;
    }

    public class ExternalItemModel(long id, string title, DateTimeOffset dateTime)
    {
        public long Id { get; set; } = id;
        public string Title { get; set; } = title;
        public DateTimeOffset DateTime { get; set; } = dateTime;
    }
}
