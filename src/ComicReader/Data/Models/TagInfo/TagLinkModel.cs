// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using ComicReader.Common.Constants;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.KVStorage;

namespace ComicReader.Data.Models.TagInfo;

internal class TagLinkModel
{
    private const string TAG = nameof(TagLinkModel);

    public static TagLinkModel Parse(string json)
    {
        TagLinkModel model = new();

        void Merge(string j, bool global)
        {
            if (string.IsNullOrEmpty(j))
            {
                return;
            }

            TagLinksJsonModel? jsonModel = null;
            try
            {
                jsonModel = JsonSerializer.Deserialize<TagLinksJsonModel>(j);
            }
            catch (JsonException e)
            {
                Logger.E(TAG, e);
            }

            if (jsonModel is null)
            {
                return;
            }

            if (jsonModel.Links != null)
            {
                foreach (TagLinkJsonModel? link in jsonModel.Links)
                {
                    if (link is null)
                    {
                        continue;
                    }

                    model.Links.Add(new()
                    {
                        Name = link.Name ?? string.Empty,
                        Link = link.Link ?? string.Empty,
                        Global = global,
                    });
                }
            }
        }

        Merge(KVDatabase.Default.GetString(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_GLOBAL_TAG_LINKS, string.Empty), true);
        Merge(json, false);
        return model;
    }

    public List<LinkModel> Links { get; set; } = [];

    public class LinkModel
    {
        public string Name { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public bool Global { get; set; } = false;
    }

    public string SerializeAndSaveGlobal()
    {
        TagLinksJsonModel localJsonModel = new()
        {
            Links = [],
        };

        TagLinksJsonModel globalJsonModel = new()
        {
            Links = [],
        };

        foreach (LinkModel link in Links)
        {
            TagLinkJsonModel linkJsonModel = new()
            {
                Name = link.Name,
                Link = link.Link,
            };

            if (link.Global)
            {
                globalJsonModel.Links.Add(linkJsonModel);
            }
            else
            {
                localJsonModel.Links.Add(linkJsonModel);
            }
        }

        string localJson = JsonSerializer.Serialize(localJsonModel);
        string globalJson = JsonSerializer.Serialize(globalJsonModel);
        KVDatabase.Default.SetString(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_GLOBAL_TAG_LINKS, globalJson);
        return localJson;
    }

    private class TagLinksJsonModel
    {
        [JsonPropertyName("Links")]
        public List<TagLinkJsonModel?>? Links { get; set; }
    }

    private class TagLinkJsonModel
    {
        [JsonPropertyName("Name")]
        public string? Name { get; set; }

        [JsonPropertyName("Link")]
        public string? Link { get; set; }
    }
}
