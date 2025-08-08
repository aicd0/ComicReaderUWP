// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.Data.Models.TagInfo;

internal class TagLinkModel
{
    private const string TAG = nameof(TagLinkModel);

    public static TagLinkModel Parse(string? json)
    {
        TagLinkModel model = new();

        if (string.IsNullOrEmpty(json))
        {
            return model;
        }

        TagLinksJsonModel? jsonModel = null;
        try
        {
            jsonModel = JsonSerializer.Deserialize<TagLinksJsonModel>(json);
        }
        catch (JsonException e)
        {
            Logger.E(TAG, e);
        }

        if (jsonModel is null)
        {
            return model;
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
                });
            }
        }

        return model;
    }

    public List<LinkModel> Links { get; set; } = [];

    public class LinkModel
    {
        public string Name { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
    }

    public string Serialize()
    {
        TagLinksJsonModel jsonModel = new()
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

            jsonModel.Links.Add(linkJsonModel);
        }

        return JsonSerializer.Serialize(jsonModel);
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
