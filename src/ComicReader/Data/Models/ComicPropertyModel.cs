// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Utils;
using ComicReader.Data.Models.Comic;
using ComicReader.SDK.Common.AppEnvironment;
using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.Data.Models;

internal class ComicPropertyModel
{
    private const string TAG = nameof(ComicPropertyModel);
    private const string PROP_TYPE_TITLE = "Title";
    private const string PROP_TYPE_PROGRESS = "Progress";
    private const string PROP_TYPE_TAG = "Tag";
    private const string PROP_TYPE_RATING = "Rating";
    private const string PROP_TYPE_COMPLETION_STATE = "CompletionState";
    private const string PROP_TYPE_LAST_READ_TIME = "LastReadTime";
    private const string PROP_TYPE_PAGES = "Pages";

    private static readonly List<PropertyTypeEnum> _properties = [
        PropertyTypeEnum.Title,
        PropertyTypeEnum.Progress,
        PropertyTypeEnum.Rating,
        PropertyTypeEnum.CompletionState,
        PropertyTypeEnum.LastReadTime,
        PropertyTypeEnum.Pages,
    ];

    private PropertyTypeEnum Type { get; set; } = PropertyTypeEnum.Title;
    private string Name { get; set; } = "";

    public string DisplayGroupName => Type switch
    {
        PropertyTypeEnum.Tag => StringResourceProvider.Instance.Tag,
        _ => "",
    };

    public string DisplayName => Type switch
    {
        PropertyTypeEnum.Title => StringResourceProvider.Instance.Title,
        PropertyTypeEnum.Progress => StringResourceProvider.Instance.Progress,
        PropertyTypeEnum.Tag => Name,
        PropertyTypeEnum.Rating => StringResourceProvider.Instance.Rating,
        PropertyTypeEnum.CompletionState => StringResourceProvider.Instance.CompletionState,
        PropertyTypeEnum.LastReadTime => StringResourceProvider.Instance.LastReadTime,
        PropertyTypeEnum.Pages => StringResourceProvider.Instance.PageCount,
        _ => "",
    };

    public override bool Equals(object? obj)
    {
        if (obj is ComicPropertyModel other)
        {
            return Type == other.Type && Name == other.Name;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Type, Name);
    }

    public JsonNode? ToJson()
    {
        var jsonModel = new JsonModel
        {
            Type = PropertyTypeToString(Type),
            Name = Name
        };
        return JsonSerializer.SerializeToNode(jsonModel);
    }

    public List<T> SortComics<T>(IEnumerable<T> items, Func<T, ComicModel> selector, ComicFilterModel.OrderMethodEnum orderMethod)
    {
        IItemSorter<ComicModel> sorter = GetComicItemSorter();
        return sorter.Sort(items, selector, orderMethod);
    }

    public List<GroupItem<T>> GroupComics<T>(IEnumerable<T> items, Func<T, ComicModel> selector,
        ComicFilterModel.OrderMethodEnum orderMethod, ComicFilterModel.FunctionTypeEnum sortingFunction,
        ComicPropertyModel? sortingProperty)
    {
        IComicGroupSorter sorter = GetComicGroupSorter();
        return sorter.GroupComics(items, selector, orderMethod, sortingFunction, sortingProperty);
    }

    private IItemSorter<ComicModel> GetComicItemSorter()
    {
        string GetConcatenatedTag(ComicModel comic)
        {
            ComicData.TagData? tagData = comic.Tags.FirstOrDefault(tag => tag.Name == Name);
            if (tagData == null)
            {
                return string.Empty;
            }
            List<string> tags = [.. tagData.Tags];
            tags.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join(' ', tags);
        }

        int CompletionStateToComparable(ComicCompletionStatusEnum state)
        {
            return state switch
            {
                ComicCompletionStatusEnum.Completed => 100,
                ComicCompletionStatusEnum.Started => 50,
                ComicCompletionStatusEnum.NotStarted => 0,
                _ => -1,
            };
        }

        return Type switch
        {
            PropertyTypeEnum.Title => new SimpleSorter<ComicModel, List<string>>(
                x => StringUtils.SmartFileNameKeySelector(x.Title ?? StringResourceProvider.Instance.Untitled), StringUtils.SmartFileNameComparer),
            PropertyTypeEnum.Progress => new SimpleSorter<ComicModel, int>(x => x.Progress),
            PropertyTypeEnum.Tag => new SimpleSorter<ComicModel, List<string>>(
                x => StringUtils.SmartFileNameKeySelector(GetConcatenatedTag(x)), StringUtils.SmartFileNameComparer),
            PropertyTypeEnum.Rating => new SimpleSorter<ComicModel, int>(x => x.Rating),
            PropertyTypeEnum.CompletionState => new SimpleSorter<ComicModel, int>(x => CompletionStateToComparable(x.CompletionState)),
            PropertyTypeEnum.LastReadTime => new SimpleSorter<ComicModel, long>(x => x.LastVisit.Ticks),
            PropertyTypeEnum.Pages => new SimpleSorter<ComicModel, int>(x => x.PageCount),
            _ => new SimpleSorter<ComicModel, long>(x => x.Id),
        };
    }

    private IComicGroupSorter GetComicGroupSorter()
    {
        string GetTitleGroupName(ComicModel comic)
        {
            string title = comic.Title.TrimStart();
            string name;
            if (string.IsNullOrEmpty(title))
            {
                name = StringResourceProvider.Instance.Untitled;
            }
            else
            {
                name = title[0].ToString().ToUpper();
            }
            return name;
        }

        string GetProgressGroupName(ComicModel comic)
        {
            int progress = Math.Min(Math.Max(comic.Progress, 0), 100);
            if (progress < 10)
            {
                return "<10%";
            }
            if (progress >= 100)
            {
                return "100%";
            }
            return $"{progress / 10 * 10}%";
        }

        IEnumerable<string> GetTagGroupNames(ComicModel comic)
        {
            ComicData.TagData? tagData = comic.Tags.FirstOrDefault(tag => tag.Name == Name);
            if (tagData == null || tagData.Tags.Count == 0)
            {
                string name = StringResourceProvider.Instance.Ungrouped;
                return [name];
            }
            return [.. tagData.Tags];
        }

        string GetRatingGroupName(ComicModel comic)
        {
            int rating = comic.Rating;
            if (rating >= 5)
            {
                return "5";
            }
            if (rating <= 0)
            {
                return StringResourceProvider.Instance.NoRating;
            }
            return rating.ToString();
        }

        string GetPagesGroupName(ComicModel comic)
        {
            int pages = comic.PageCount;
            if (pages <= 0)
            {
                return StringResourceProvider.Instance.Ungrouped;
            }

            if (pages < 10)
            {
                return "<10";
            }

            if (pages < 100)
            {
                return $"{pages / 10 * 10}";
            }

            if (pages < 1000)
            {
                return $"{pages / 100 * 100}";
            }

            return "1000+";
        }

        string GetCompletionStatusGroupName(ComicModel comic)
        {
            return comic.CompletionState switch
            {
                ComicCompletionStatusEnum.Completed => StringResourceProvider.Instance.CompletionStatusFinished,
                ComicCompletionStatusEnum.Started => StringResourceProvider.Instance.CompletionStatusReading,
                ComicCompletionStatusEnum.NotStarted => StringResourceProvider.Instance.CompletionStatusUnread,
                _ => StringResourceProvider.Instance.Ungrouped,
            };
        }

        int GetCompletionStatusGroupSortingKey(ComicModel comic)
        {
            return comic.CompletionState switch
            {
                ComicCompletionStatusEnum.Completed => 3,
                ComicCompletionStatusEnum.Started => 2,
                ComicCompletionStatusEnum.NotStarted => 1,
                _ => 0,
            };
        }

        string GetLastReadTimeGroupName(ComicModel comic)
        {
            DateTimeOffset lastReadTime = comic.LastVisit;
            if (lastReadTime == DateTimeOffset.MinValue)
            {
                return StringResourceProvider.Instance.Ungrouped;
            }
            return lastReadTime.ToString("D", EnvironmentProvider.Instance.GetCurrentAppLanguageInfo());
        }

        return Type switch
        {
            PropertyTypeEnum.Title => new ComicGroupSorter(x => [GetTitleGroupName(x)], new GroupSorter<GroupSortingKeySelectorParams, List<string>>(
                x => StringUtils.SmartFileNameKeySelector(x.GroupName), comparer: StringUtils.SmartFileNameComparer)),
            PropertyTypeEnum.Progress => new ComicGroupSorter(x => [GetProgressGroupName(x)], new GroupSorter<GroupSortingKeySelectorParams, int>(
                x => Math.Clamp(x.Items[0].Progress, 0, 100))),
            PropertyTypeEnum.Tag => new ComicGroupSorter(GetTagGroupNames, new GroupSorter<GroupSortingKeySelectorParams, List<string>>(
                x => StringUtils.SmartFileNameKeySelector(x.GroupName), comparer: StringUtils.SmartFileNameComparer)),
            PropertyTypeEnum.Rating => new ComicGroupSorter(x => [GetRatingGroupName(x)], new GroupSorter<GroupSortingKeySelectorParams, int>(
                x => Math.Clamp(x.Items[0].Rating, 0, 5))),
            PropertyTypeEnum.CompletionState => new ComicGroupSorter(x => [GetCompletionStatusGroupName(x)], new GroupSorter<GroupSortingKeySelectorParams, int>(
                x => GetCompletionStatusGroupSortingKey(x.Items[0]))),
            PropertyTypeEnum.LastReadTime => new ComicGroupSorter(x => [GetLastReadTimeGroupName(x)], new GroupSorter<GroupSortingKeySelectorParams, long>(
                x => x.Items[0].LastVisit.Ticks)),
            PropertyTypeEnum.Pages => new ComicGroupSorter(x => [GetPagesGroupName(x)], new GroupSorter<GroupSortingKeySelectorParams, int>(
                x => x.Items[0].PageCount)),
            _ => new ComicGroupSorter(x => [new(StringResourceProvider.Instance.Ungrouped)], new GroupSorter<GroupSortingKeySelectorParams, int>(
                x => 0)),
        };
    }

    public static ComicPropertyModel? FromJson(JsonNode? model)
    {
        if (model == null)
        {
            return null;
        }

        JsonModel? jsonModel = null;
        try
        {
            jsonModel = JsonSerializer.Deserialize<JsonModel>(model);
        }
        catch (JsonException e)
        {
            Logger.F(TAG, "FromJson", e);
        }
        if (jsonModel == null)
        {
            return null;
        }

        return new ComicPropertyModel
        {
            Type = StringToPropertyType(jsonModel.Type),
            Name = jsonModel.Name ?? "",
        };
    }

    private static string PropertyTypeToString(PropertyTypeEnum value)
    {
        return value switch
        {
            PropertyTypeEnum.Title => PROP_TYPE_TITLE,
            PropertyTypeEnum.Progress => PROP_TYPE_PROGRESS,
            PropertyTypeEnum.Tag => PROP_TYPE_TAG,
            PropertyTypeEnum.Rating => PROP_TYPE_RATING,
            PropertyTypeEnum.CompletionState => PROP_TYPE_COMPLETION_STATE,
            PropertyTypeEnum.LastReadTime => PROP_TYPE_LAST_READ_TIME,
            PropertyTypeEnum.Pages => PROP_TYPE_PAGES,
            _ => PROP_TYPE_TITLE,
        };
    }

    private static PropertyTypeEnum StringToPropertyType(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return PropertyTypeEnum.Title;
        }

        return value switch
        {
            PROP_TYPE_TITLE => PropertyTypeEnum.Title,
            PROP_TYPE_PROGRESS => PropertyTypeEnum.Progress,
            PROP_TYPE_TAG => PropertyTypeEnum.Tag,
            PROP_TYPE_RATING => PropertyTypeEnum.Rating,
            PROP_TYPE_COMPLETION_STATE => PropertyTypeEnum.CompletionState,
            PROP_TYPE_LAST_READ_TIME => PropertyTypeEnum.LastReadTime,
            PROP_TYPE_PAGES => PropertyTypeEnum.Pages,
            _ => PropertyTypeEnum.Title,
        };
    }

    public static async Task<List<ComicPropertyModel>> GetProperties()
    {
        var properties = new List<ComicPropertyModel>();
        foreach (PropertyTypeEnum propertyType in _properties)
        {
            properties.Add(new ComicPropertyModel
            {
                Type = propertyType,
            });
        }

        {
            List<string> tagCategories = await ComicModel.GetAllTagCategories();
            foreach (string tag in tagCategories)
            {
                properties.Add(new ComicPropertyModel
                {
                    Type = PropertyTypeEnum.Tag,
                    Name = tag,
                });
            }
        }

        return properties;
    }

    private class JsonModel
    {
        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        [JsonPropertyName("Name")]
        public string? Name { get; set; }
    }

    public class GroupItem<T>(List<T> items, string name)
    {
        public List<T> Items { get; } = items;
        public string Name { get; } = name;
        public string Description { get; set; } = string.Empty;
    }

    private interface IComicGroupSorter
    {
        List<GroupItem<T>> GroupComics<T>(IEnumerable<T> items, Func<T, ComicModel> selector,
            ComicFilterModel.OrderMethodEnum orderMethod, ComicFilterModel.FunctionTypeEnum sortingFunction,
            ComicPropertyModel? sortingProperty);
    }

    private class ComicGroupSorter(Func<ComicModel, IEnumerable<string>> groupNameSelector, IItemSorterWithKeyInfo<GroupSortingKeySelectorParams, string> defaultSorter) : IComicGroupSorter
    {
        private readonly Func<ComicModel, IEnumerable<string>> GroupNameSelector = groupNameSelector;
        private readonly IItemSorterWithKeyInfo<GroupSortingKeySelectorParams, string> DefaultGroupSorter = defaultSorter;

        public List<GroupItem<T>> GroupComics<T>(IEnumerable<T> items, Func<T, ComicModel> selector,
            ComicFilterModel.OrderMethodEnum orderMethod, ComicFilterModel.FunctionTypeEnum sortingFunction,
            ComicPropertyModel? sortingProperty)
        {
            Dictionary<string, List<T>> groupMap = [];
            foreach (T item in items)
            {
                IEnumerable<string> groupNames = GroupNameSelector(selector(item));
                foreach (string name in groupNames)
                {
                    if (!groupMap.TryGetValue(name, out List<T>? group))
                    {
                        group = [];
                        groupMap[name] = group;
                    }
                    group.Add(item);
                }
            }

            List<GroupItem<T>> comicGroups = [];
            foreach (KeyValuePair<string, List<T>> p in groupMap)
            {
                comicGroups.Add(new(p.Value, p.Key));
            }

            IItemSorterWithKeyInfo<GroupSortingKeySelectorParams, string> sorter = GetSorter(sortingFunction, sortingProperty);
            return sorter.Sort(comicGroups, x => new(x.Name, x.Items.ConvertAll(y => selector(y))), orderMethod,
                (m, t) => m.Description = string.IsNullOrEmpty(t) ? $"({m.Items.Count})" : $"({t})");
        }

        private IItemSorterWithKeyInfo<GroupSortingKeySelectorParams, string> GetSorter(ComicFilterModel.FunctionTypeEnum sortingFunction, ComicPropertyModel? sortingProperty)
        {
            double? PropertyToNumber(ComicModel comic)
            {
                return sortingProperty?.Type switch
                {
                    PropertyTypeEnum.Title => comic.Title.Length,
                    PropertyTypeEnum.Tag => sortingProperty.Name.Length,
                    PropertyTypeEnum.Progress => Math.Clamp(comic.Progress, 0, 100),
                    PropertyTypeEnum.Rating => comic.Rating > 0 ? comic.Rating : null,
                    PropertyTypeEnum.CompletionState => (int)comic.CompletionState,
                    PropertyTypeEnum.LastReadTime => comic.LastVisit != DateTimeOffset.MinValue ? comic.LastVisit.ToUnixTimeMilliseconds() : null,
                    PropertyTypeEnum.Pages => comic.PageCount > 0 ? comic.PageCount : null,
                    _ => null,
                };
            }

            string NumberToString(double value)
            {
                // If the value is an integer, return as is
                if (value == Math.Truncate(value))
                {
                    return value.ToString(CultureInfo.InvariantCulture);
                }

                // Use "G17" to get the full precision, then trim to at most 2 digits after decimal
                string str = value.ToString("G17", CultureInfo.InvariantCulture);

                int dotIndex = str.IndexOf('.');
                if (dotIndex < 0)
                {
                    return str;
                }

                // Find the end of the valid digits after decimal
                int endIndex = dotIndex + 1;
                int digits = 0;
                while (endIndex < str.Length && digits < 2)
                {
                    char c = str[endIndex];
                    if (char.IsDigit(c))
                    {
                        digits++;
                    }

                    endIndex++;
                }

                // If there are more digits, trim
                if (endIndex < str.Length)
                {
                    str = str[..endIndex];
                }

                // Remove trailing zeros after decimal
                if (digits > 0)
                {
                    // Remove trailing zeros, but keep at least one digit after decimal
                    int lastNonZero = str.Length - 1;
                    while (lastNonZero > dotIndex + 1 && str[lastNonZero] == '0')
                    {
                        lastNonZero--;
                    }

                    str = str.Substring(0, lastNonZero + 1);
                }

                // Remove trailing decimal point if no digits after
                if (str.EndsWith('.'))
                {
                    str = str[..^1];
                }

                return str;
            }

            return sortingFunction switch
            {
                ComicFilterModel.FunctionTypeEnum.None => DefaultGroupSorter,
                ComicFilterModel.FunctionTypeEnum.ItemCount => new GroupSorter<GroupSortingKeySelectorParams, int>(x => x.Items.Count, x => x.ToString()),
                ComicFilterModel.FunctionTypeEnum.Max => new GroupSorter<GroupSortingKeySelectorParams, double>(x => x.Items.Max(PropertyToNumber) ?? 0, NumberToString),
                ComicFilterModel.FunctionTypeEnum.Min => new GroupSorter<GroupSortingKeySelectorParams, double>(x => x.Items.Min(PropertyToNumber) ?? 0, NumberToString),
                ComicFilterModel.FunctionTypeEnum.Sum => new GroupSorter<GroupSortingKeySelectorParams, double>(x => x.Items.Sum(PropertyToNumber) ?? 0, NumberToString),
                ComicFilterModel.FunctionTypeEnum.Average => new GroupSorter<GroupSortingKeySelectorParams, double>(x => x.Items.Average(PropertyToNumber) ?? 0, NumberToString),
                _ => DefaultGroupSorter,
            };
        }
    }

    private interface IItemSorter<K>
    {
        List<T> Sort<T>(IEnumerable<T> items, Func<T, K> selector, ComicFilterModel.OrderMethodEnum orderMethod);
    }

    private interface IItemSorterWithKeyInfo<K, M> : IItemSorter<K>
    {
        List<T> Sort<T>(IEnumerable<T> items, Func<T, K> selector, ComicFilterModel.OrderMethodEnum orderMethod, Action<T, M> keyBinder);
    }

    private class SimpleSorter<A, B>(Func<A, B> keySelector, IComparer<B>? comparer = null) : IItemSorter<A>
    {
        protected IComparer<B> Comparer { get; } = comparer ?? Comparer<B>.Default;
        protected Func<A, B> KeySelector { get; } = keySelector;

        public List<T> Sort<T>(IEnumerable<T> items, Func<T, A> selector, ComicFilterModel.OrderMethodEnum orderMethod)
        {
            switch (orderMethod)
            {
                case ComicFilterModel.OrderMethodEnum.Ascending:
                    return [.. items.OrderBy(x => KeySelector(selector(x)), Comparer)];
                case ComicFilterModel.OrderMethodEnum.Descending:
                    return [.. items.OrderByDescending(x => KeySelector(selector(x)), Comparer)];
                case ComicFilterModel.OrderMethodEnum.Shuffle:
                    return [.. items.OrderBy(_ => Random.Shared.Next())];
                default:
                    goto case ComicFilterModel.OrderMethodEnum.Ascending;
            }
        }
    }

    private class GroupSorter<A, B>(Func<A, B> keySelector, Func<B, string>? keyInfoConverter = null, IComparer<B>? comparer = null) : SimpleSorter<A, B>(keySelector, comparer), IItemSorterWithKeyInfo<A, string>
    {
        private Func<B, string> KeyInfoConverter { get; } = keyInfoConverter ?? (_ => string.Empty);

        public List<T> Sort<T>(IEnumerable<T> items, Func<T, A> selector, ComicFilterModel.OrderMethodEnum orderMethod, Action<T, string> keyBinder)
        {
            B GroupKeySelector(T item)
            {
                B key = KeySelector(selector(item));
                keyBinder(item, KeyInfoConverter(key));
                return key;
            }

            switch (orderMethod)
            {
                case ComicFilterModel.OrderMethodEnum.Ascending:
                    return [.. items.OrderBy(GroupKeySelector, Comparer)];
                case ComicFilterModel.OrderMethodEnum.Descending:
                    return [.. items.OrderByDescending(GroupKeySelector, Comparer)];
                case ComicFilterModel.OrderMethodEnum.Shuffle:
                    return [.. items.OrderBy(_ => Random.Shared.Next())];
                default:
                    goto case ComicFilterModel.OrderMethodEnum.Ascending;
            }
        }
    }

    private class GroupSortingKeySelectorParams(string groupName, List<ComicModel> items)
    {
        public string GroupName { get; } = groupName;
        public List<ComicModel> Items { get; } = items;
    }

    private enum PropertyTypeEnum
    {
        CompletionState,
        Progress,
        Rating,
        Tag,
        Title,
        LastReadTime,
        Pages,
    }
}
