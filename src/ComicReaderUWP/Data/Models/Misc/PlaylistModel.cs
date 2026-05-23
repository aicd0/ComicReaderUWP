// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Data.Models.Comic;

namespace ComicReaderUWP.Data.Models.Misc;

internal class PlaylistModel
{
    private const string TAG = nameof(PlaylistModel);

    public static PlaylistModel CreateEmpty()
    {
        return new([]);
    }

    public static async Task<PlaylistModel?> CreateFromSerializedString(string serializedPlaylist)
    {
        List<PlaylistItemJsonModel?>? jsonItems;
        try
        {
            jsonItems = JsonSerializer.Deserialize<List<PlaylistItemJsonModel?>>(serializedPlaylist);
        }
        catch (JsonException ex)
        {
            Logger.E(TAG, ex);
            return null;
        }

        if (jsonItems is null)
        {
            return null;
        }

        return await CreateFromBuilder(jsonItems.Where(x => x is not null).Select(x => x!));
    }

    private static async Task<PlaylistModel> CreateFromBuilder(IEnumerable<PlaylistItemJsonModel> builder)
    {
        List<long> comicIds = [];
        foreach (PlaylistItemJsonModel jsonItem in builder)
        {
            if (!jsonItem.IsExternal.HasValue)
            {
                continue;
            }

            if (!jsonItem.IsExternal.Value)
            {
                if (!jsonItem.ComicId.HasValue)
                {
                    continue;
                }

                comicIds.Add(jsonItem.ComicId.Value);
            }
        }

        List<ComicModel> libraryComics = await ComicModel.BatchFromId(TAG, comicIds);
        var comicIdMap = new Dictionary<long, ComicModel>();
        foreach (ComicModel comic in libraryComics)
        {
            comicIdMap[comic.Id] = comic;
        }

        List<PlaylistItem> items = [];
        foreach (PlaylistItemJsonModel jsonItem in builder)
        {
            if (!jsonItem.IsExternal.HasValue || string.IsNullOrEmpty(jsonItem.Id))
            {
                continue;
            }

            ComicModel? comic = null;
            if (jsonItem.IsExternal.Value)
            {
                comic = await ComicModel.FromExternalLocation(jsonItem.Location ?? string.Empty);
            }
            else if (jsonItem.ComicId.HasValue)
            {
                comicIdMap.TryGetValue(jsonItem.ComicId.Value, out comic);
            }

            if (comic is not null)
            {
                items.Add(new()
                {
                    Comic = comic,
                    Id = jsonItem.Id,
                });
            }
        }

        return new(items);
    }

    private readonly List<PlaylistItem> _items;

    public IReadOnlyList<PlaylistItem> Items => _items;

    private PlaylistModel(List<PlaylistItem> items)
    {
        _items = items;
    }

    public Builder ToBuilder()
    {
        return Builder.Create().AddComics(_items.Select(x => x.Comic));
    }

    public string ToSerializedString()
    {
        IEnumerable<PlaylistItemJsonModel> jsonItems = _items
            .Select(x => new PlaylistItemJsonModel()
            {
                Id = x.Id,
                IsExternal = x.Comic.IsExternal,
                ComicId = x.Comic.Id,
                Location = x.Comic.Location,
            });
        return JsonSerializer.Serialize(new List<PlaylistItemJsonModel>(jsonItems));
    }

    public class PlaylistItem
    {
        public required string Id { get; init; }
        public required ComicModel Comic { get; init; }
    }

    private class PlaylistItemJsonModel
    {
        [JsonPropertyName("Id")]
        public string? Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("IsExternal")]
        public bool? IsExternal { get; set; }

        [JsonPropertyName("ComicId")]
        public long? ComicId { get; set; }

        [JsonPropertyName("Location")]
        public string? Location { get; set; }
    }

    public class Builder
    {
        public static Builder Create()
        {
            return new();
        }

        private readonly List<PlaylistItemJsonModel> _comics = [];

        private Builder() { }

        public Builder AddComics(IEnumerable<ComicModel> comics)
        {
            foreach (ComicModel comic in comics)
            {
                AddComic(comic);
            }

            return this;
        }

        public string EnsureComic(ComicModel comic)
        {
            string? itemId = IdOf(comic);
            if (string.IsNullOrEmpty(itemId))
            {
                var item = new PlaylistItemJsonModel()
                {
                    IsExternal = comic.IsExternal,
                    Location = comic.Location,
                    ComicId = comic.Id
                };
                itemId = item.Id!;
                _comics.Insert(0, item);
            }

            return itemId;
        }

        public Builder AddComic(ComicModel comic)
        {
            _comics.Add(new PlaylistItemJsonModel()
            {
                IsExternal = comic.IsExternal,
                Location = comic.Location,
                ComicId = comic.Id
            });
            return this;
        }

        public Builder AddComicIds(IEnumerable<long> ids)
        {
            foreach (long id in ids)
            {
                AddComicId(id);
            }

            return this;
        }

        public Builder AddComicId(long id)
        {
            _comics.Add(new PlaylistItemJsonModel()
            {
                IsExternal = false,
                ComicId = id,
            });
            return this;
        }

        public Builder AddComicLocation(string location)
        {
            _comics.Add(new PlaylistItemJsonModel()
            {
                IsExternal = true,
                Location = location,
            });
            return this;
        }

        private string? IdOf(ComicModel comic)
        {
            foreach (PlaylistItemJsonModel item in _comics)
            {
                if (comic.IsExternal != item.IsExternal)
                {
                    continue;
                }

                if (comic.IsExternal)
                {
                    if (comic.Location != item.Location)
                    {
                        continue;
                    }
                }
                else
                {
                    if (comic.Id != item.ComicId)
                    {
                        continue;
                    }
                }

                return item.Id;
            }

            return null;
        }

        public string ToSerializedString()
        {
            return JsonSerializer.Serialize(_comics);
        }
    }
}
