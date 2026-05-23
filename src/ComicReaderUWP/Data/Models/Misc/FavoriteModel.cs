// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

using ComicReaderUWP.Common.Misc;
using ComicReaderUWP.Core.Database.Misc;

namespace ComicReaderUWP.Data.Models.Misc;

class FavoriteModel : JsonDatabase<FavoriteModel.JsonModel>
{
    public class JsonModel
    {
        [JsonPropertyName("children")]
        public List<JsonNodeModel?>? Children { get; set; }
    }

    public class JsonNodeModel
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("children")]
        public List<JsonNodeModel?>? Children { get; set; }
    }

    public static readonly FavoriteModel Instance = new();

    private FavoriteModel() : base("favorites.json") { }

    protected override JsonModel InitializeModel(JsonModel? model)
    {
        model ??= new();
        return model;
    }

    public ExternalModel GetModel()
    {
        return Read(ConvertToExternalModel);
    }

    public void UpdateModel(ExternalModel model)
    {
        Write(m =>
        {
            m.Children = [];
            foreach (ExternalNodeModel node in model.Children)
            {
                m.Children.Add(ConvertToJsonModel(node));
            }
        });
        Save();
        DispatchUpdateEvent();
    }

    public ExternalNodeModel? FromId(long id)
    {
        JsonNodeModel? model = Read(model => FromIdNoLock(model, id));
        return ConvertToExternalModel(model);
    }

    public bool RemoveWithId(long id, bool sendEvent)
    {
        bool helper(List<JsonNodeModel?>? e)
        {
            if (e is null)
            {
                return false;
            }

            for (int i = 0; i < e.Count; ++i)
            {
                JsonNodeModel? node = e[i];
                if (node is null)
                {
                    continue;
                }

                if (node.Type == "i")
                {
                    if (node.Id == id)
                    {
                        e.RemoveAt(i);
                        return true;
                    }
                }
                else if (helper(node.Children))
                {
                    return true;
                }
            }

            return false;
        }

        bool result = Write(model => helper(model.Children));
        Save();

        if (sendEvent)
        {
            DispatchUpdateEvent();
        }

        return result;
    }

    public void BatchRemoveWithId(List<long> ids)
    {
        if (ids == null || ids.Count == 0)
        {
            return;
        }

        bool helper(List<JsonNodeModel?>? e)
        {
            if (e is null)
            {
                return false;
            }

            bool updated = false;
            for (int i = e.Count - 1; i >= 0; --i)
            {
                JsonNodeModel? node = e[i];
                if (node is null)
                {
                    continue;
                }

                if (node.Type == "i")
                {
                    if (node.Id.HasValue && ids.Contains(node.Id.Value))
                    {
                        e.RemoveAt(i);
                        updated = true;
                    }
                }
                else if (helper(node.Children))
                {
                    updated = true;
                }
            }

            return updated;
        }

        bool updated = Write(model => helper(model.Children));
        if (updated)
        {
            Save();
            DispatchUpdateEvent();
        }
    }

    public void Add(long id, string title, bool sendEvent)
    {
        bool updated = Write(model =>
        {
            JsonNodeModel? node = FromIdNoLock(model, id);
            if (node != null)
            {
                return false;
            }

            node = new JsonNodeModel
            {
                Type = "i",
                Name = title,
                Id = id
            };
            model.Children ??= [];
            model.Children.Add(node);
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

    public void BatchAdd(List<FavoriteItem> items)
    {
        if (items == null || items.Count == 0)
        {
            return;
        }

        bool updated = Write(model =>
        {
            bool updated = false;
            foreach (FavoriteItem item in items)
            {
                JsonNodeModel? node = FromIdNoLock(model, item.Id);
                if (node != null)
                {
                    continue;
                }

                node = new JsonNodeModel
                {
                    Type = "i",
                    Name = item.Title,
                    Id = item.Id
                };
                model.Children ??= [];
                model.Children.Add(node);
                updated = true;
            }
            return updated;
        });

        if (updated)
        {
            Save();
            DispatchUpdateEvent();
        }
    }

    private static JsonNodeModel? FromIdNoLock(JsonModel model, long id)
    {
        JsonNodeModel? helper(List<JsonNodeModel?>? e)
        {
            if (e is null)
            {
                return null;
            }

            foreach (JsonNodeModel? node in e)
            {
                if (node is null)
                {
                    continue;
                }

                if (node.Type == "i")
                {
                    if (node.Id == id)
                    {
                        return node;
                    }
                }
                else
                {
                    JsonNodeModel? result = helper(node.Children);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }

            return null;
        }

        return helper(model.Children);
    }

    private static void DispatchUpdateEvent()
    {
        GlobalEvent.Instance.FavoriteUpdated.Emit(0);
    }

    private static ExternalModel ConvertToExternalModel(JsonModel model)
    {
        var children = new List<ExternalNodeModel>();
        if (model.Children is not null)
        {
            foreach (JsonNodeModel? child in model.Children)
            {
                ExternalNodeModel? childModel = ConvertToExternalModel(child);
                if (childModel is null)
                {
                    continue;
                }

                children.Add(childModel);
            }
        }

        return new ExternalModel(children);
    }

    private static ExternalNodeModel? ConvertToExternalModel(JsonNodeModel? node)
    {
        if (node is null || !node.Id.HasValue || string.IsNullOrEmpty(node.Type) || string.IsNullOrEmpty(node.Name))
        {
            return null;
        }

        var children = new List<ExternalNodeModel>();
        if (node.Children is not null)
        {
            foreach (JsonNodeModel? child in node.Children)
            {
                ExternalNodeModel? childModel = ConvertToExternalModel(child);
                if (childModel is null)
                {
                    continue;
                }

                children.Add(childModel);
            }
        }

        return new ExternalNodeModel(node.Type, node.Name, node.Id.Value, children);
    }

    private static JsonNodeModel ConvertToJsonModel(ExternalNodeModel node)
    {
        var children = new List<JsonNodeModel?>();
        foreach (ExternalNodeModel child in node.Children)
        {
            children.Add(ConvertToJsonModel(child));
        }

        return new JsonNodeModel
        {
            Type = node.Type,
            Name = node.Name,
            Id = node.Id,
            Children = children
        };
    }

    public class ExternalModel(List<ExternalNodeModel> nodes)
    {
        public List<ExternalNodeModel> Children { get; set; } = nodes;
    }

    public class ExternalNodeModel(string type, string name, long id, List<ExternalNodeModel> nodes)
    {
        public string Type { get; set; } = type;
        public string Name { get; set; } = name;
        public long Id { get; set; } = id;
        public List<ExternalNodeModel> Children { get; set; } = nodes;
    }

    public struct FavoriteItem
    {
        public string Title;
        public long Id;
    }
}
