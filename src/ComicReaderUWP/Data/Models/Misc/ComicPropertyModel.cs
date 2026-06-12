// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Plugins;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.AppEnvironment;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.SDK.Plugins.Comic;
using ComicReaderUWP.SDK.Plugins.Property;

namespace ComicReaderUWP.Data.Models.Misc;

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
    private const string PROP_TYPE_PLUGIN_VIRTUAL_PROPERTY = "PluginVirtualProperty";

    private static readonly List<PropertyTypeEnum> _properties = [
        PropertyTypeEnum.Title,
        PropertyTypeEnum.Progress,
        PropertyTypeEnum.Rating,
        PropertyTypeEnum.CompletionState,
        PropertyTypeEnum.LastReadTime,
        PropertyTypeEnum.Pages,
    ];

    private PropertyTypeEnum Type { get; init; } = PropertyTypeEnum.Title;
    private string Name { get; init; } = string.Empty;

    public string DisplayGroupName => Type switch
    {
        PropertyTypeEnum.Tag => StringResourceProvider.Instance.Tag,
        _ => string.Empty,
    };

    private string _displayName = string.Empty;
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(_displayName))
            {
                return _displayName;
            }

            return Type switch
            {
                PropertyTypeEnum.Title => StringResourceProvider.Instance.Title,
                PropertyTypeEnum.Progress => StringResourceProvider.Instance.Progress,
                PropertyTypeEnum.Tag => Name,
                PropertyTypeEnum.Rating => StringResourceProvider.Instance.Rating,
                PropertyTypeEnum.CompletionState => StringResourceProvider.Instance.CompletionState,
                PropertyTypeEnum.LastReadTime => StringResourceProvider.Instance.LastReadTime,
                PropertyTypeEnum.Pages => StringResourceProvider.Instance.PageCount,
                _ => string.Empty,
            };
        }
        set => _displayName = value;
    }

    private bool _pluginPropertyInitialized = false;
    private IVirtualProperty<IComicModel>? _pluginProperty;
    private IVirtualProperty<IComicModel>? PluginProperty
    {
        get
        {
            if (_pluginPropertyInitialized)
            {
                return _pluginProperty;
            }

            _pluginPropertyInitialized = true;
            if (Type != PropertyTypeEnum.PluginVirtualProperty)
            {
                return null;
            }

            string[] pieces = Name.Split(':');
            if (pieces.Length != 2)
            {
                return null;
            }

            string pluginName = pieces[0];
            string propertyName = pieces[1];
            PluginContext? plugin = PluginManager.Instance.GetActivePlugin(pluginName);
            if (plugin is null)
            {
                return null;
            }

            _pluginProperty = plugin.GetAllComicVirtualProperties().FirstOrDefault(x => x.Name == propertyName);
            return _pluginProperty;
        }
    }

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
        IEnumerable<T> preSorted = items.OrderBy(x => StringUtils.SmartFileNameKeySelector(selector(x).Title ?? StringResourceProvider.Instance.Untitled), StringUtils.SmartFileNameComparer);
        IVirtualPropertySorter<IComicModel>? pluginSorter = PluginProperty?.CreateSorter(preSorted.Select(selector));
        IItemSorter<ComicModel> sorter = GetComicItemSorter(orderMethod, pluginSorter);
        return sorter.Sort(preSorted, selector);
    }

    public List<GroupItem<T>> GroupComics<T>(IEnumerable<T> items, Func<T, ComicModel> selector,
        ComicFilterModel.OrderMethodEnum orderMethod, ComicFilterModel.FunctionTypeEnum sortingFunction,
        ComicPropertyModel? sortingProperty)
    {
        IEnumerable<T> preSorted = items.OrderBy(x => StringUtils.SmartFileNameKeySelector(selector(x).Title ?? StringResourceProvider.Instance.Untitled), StringUtils.SmartFileNameComparer);
        IVirtualPropertySorter<IComicModel>? pluginSorter = PluginProperty?.CreateSorter(preSorted.Select(selector));
        IVirtualPropertySorter<IComicModel>? sortingPropertyPluginSorter = sortingProperty?.PluginProperty?.CreateSorter(preSorted.Select(selector));
        ComicGrouper grouper = GetComicGrouper(orderMethod, pluginSorter, sortingFunction, sortingProperty, sortingPropertyPluginSorter);
        return grouper.GroupComics(preSorted, selector);
    }

    private IItemSorter<ComicModel> GetComicItemSorter(ComicFilterModel.OrderMethodEnum orderMethod, IVirtualPropertySorter<IComicModel>? pluginSorter)
    {
        switch (orderMethod)
        {
            case ComicFilterModel.OrderMethodEnum.Shuffle:
            case ComicFilterModel.OrderMethodEnum.ShuffleStable:
                int IdSelector(ComicModel x) => HashUtils.GetXxHash64Int(x.Id);
                return new RandomSorter<ComicModel>(orderMethod, IdSelector);
            default:
                break;
        }

        string GetConcatenatedTag(ComicModel comic)
        {
            ComicHandle.TagData? tagData = comic.Tags.FirstOrDefault(tag => tag.Name == Name);
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

        IItemSorter<ComicModel> CreateSorter<T>(Func<ComicModel, T> keySelector, IComparer<T>? keyComparer = null)
        {
            return new BasicItemSorter<ComicModel, T>(orderMethod, keySelector, keyComparer);
        }

        IItemSorter<ComicModel> CreateFallbackSorter()
        {
            Logger.F(TAG, "Using fallback sorter");
            return CreateSorter(x => x.Id);
        }

        IItemSorter<ComicModel> CreatePluginPropertySorter()
        {
            if (pluginSorter is null)
            {
                return CreateFallbackSorter();
            }

            return new PluginPropertyItemSorter(pluginSorter, orderMethod);
        }

        return Type switch
        {
            PropertyTypeEnum.Title => CreateSorter(x => StringUtils.SmartFileNameKeySelector(x.Title ?? StringResourceProvider.Instance.Untitled), keyComparer: StringUtils.SmartFileNameComparer),
            PropertyTypeEnum.Progress => CreateSorter(x => x.Progress),
            PropertyTypeEnum.Tag => CreateSorter(x => StringUtils.SmartFileNameKeySelector(GetConcatenatedTag(x)), keyComparer: StringUtils.SmartFileNameComparer),
            PropertyTypeEnum.Rating => CreateSorter(x => x.Rating),
            PropertyTypeEnum.CompletionState => CreateSorter(x => CompletionStateToComparable(x.CompletionState)),
            PropertyTypeEnum.LastReadTime => CreateSorter(x => x.LastVisit.Ticks),
            PropertyTypeEnum.Pages => CreateSorter(x => x.PageCount),
            PropertyTypeEnum.PluginVirtualProperty => CreatePluginPropertySorter(),
            _ => CreateFallbackSorter(),
        };
    }

    private ComicGrouper GetComicGrouper(ComicFilterModel.OrderMethodEnum orderMethod, IVirtualPropertySorter<IComicModel>? pluginSorter,
        ComicFilterModel.FunctionTypeEnum sortingFunction, ComicPropertyModel? sortingProperty, IVirtualPropertySorter<IComicModel>? sortingPropertyPluginSorter)
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
            int progress = comic.Progress;
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
            ComicHandle.TagData? tagData = comic.Tags.FirstOrDefault(tag => tag.Name == Name);
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
            if (rating < 0)
            {
                return StringResourceProvider.Instance.NoRating;
            }

            int level = Math.Min(rating, 99) / 10;
            if (level <= 0)
            {
                return "<0.5";
            }

            return (level * 0.5F).ToString("0.#");
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

        IGroupSorter<ComicGroup, string> CreateSorter<T>(Func<ComicGroup, T> keySelector, Func<T, string>? keyInfoConverter = null, IComparer<T>? keyComparer = null)
        {
            return new BasicGroupSorter<ComicGroup, T>(orderMethod, keySelector, keyInfoConverter, keyComparer);
        }

        IGroupSorter<ComicGroup, string> CreateFallbackSorter()
        {
            Logger.F(TAG, "Using fallback sorter");
            return CreateSorter(x => 0);
        }

        IGroupSorter<ComicGroup, string> CreatePluginPropertySorter()
        {
            if (pluginSorter is null)
            {
                return CreateFallbackSorter();
            }

            return new PluginPropertyGroupSorter(pluginSorter, orderMethod);
        }

        IGroupSorter<ComicGroup, string> CreateDefaultSorter()
        {
            switch (orderMethod)
            {
                case ComicFilterModel.OrderMethodEnum.Shuffle:
                case ComicFilterModel.OrderMethodEnum.ShuffleStable:
                    int IdSelector(ComicGroup x) => HashUtils.GetXxHash64Int(x.GroupName);
                    return new RandomSorter<ComicGroup>(orderMethod, IdSelector);
                default:
                    break;
            }

            return Type switch
            {
                PropertyTypeEnum.Title => CreateSorter(x => StringUtils.SmartFileNameKeySelector(x.GroupName), keyComparer: StringUtils.SmartFileNameComparer),
                PropertyTypeEnum.Progress => CreateSorter(x => x.Items[0].Progress),
                PropertyTypeEnum.Tag => CreateSorter(x => StringUtils.SmartFileNameKeySelector(x.GroupName), keyComparer: StringUtils.SmartFileNameComparer),
                PropertyTypeEnum.Rating => CreateSorter(x => x.Items[0].Rating),
                PropertyTypeEnum.CompletionState => CreateSorter(x => GetCompletionStatusGroupSortingKey(x.Items[0])),
                PropertyTypeEnum.LastReadTime => CreateSorter(x => x.Items[0].LastVisit.Ticks),
                PropertyTypeEnum.Pages => CreateSorter(x => x.Items[0].PageCount),
                PropertyTypeEnum.PluginVirtualProperty => CreatePluginPropertySorter(),
                _ => CreateFallbackSorter(),
            };
        }

        IGroupSorter<ComicGroup, string> defaultSorter = CreateDefaultSorter();

        ComicGrouper CreateGrouper(Func<ComicModel, IEnumerable<string>> groupNameSelector)
        {
            IGroupSorter<ComicGroup, string> groupSorter = GetGroupSorter(orderMethod, sortingFunction, sortingProperty, sortingPropertyPluginSorter, defaultSorter);
            return new(groupSorter, groupNameSelector);
        }

        ComicGrouper CreateFallbackGrouper()
        {
            Logger.F(TAG, "Using fallback grouper");
            return CreateGrouper(x => [new(StringResourceProvider.Instance.Ungrouped)]);
        }

        ComicGrouper CreatePluginPropertyGrouper()
        {
            if (pluginSorter is null)
            {
                return CreateFallbackGrouper();
            }

            return CreateGrouper(pluginSorter.GetGroupNames);
        }

        return Type switch
        {
            PropertyTypeEnum.Title => CreateGrouper(x => [GetTitleGroupName(x)]),
            PropertyTypeEnum.Progress => CreateGrouper(x => [GetProgressGroupName(x)]),
            PropertyTypeEnum.Tag => CreateGrouper(GetTagGroupNames),
            PropertyTypeEnum.Rating => CreateGrouper(x => [GetRatingGroupName(x)]),
            PropertyTypeEnum.CompletionState => CreateGrouper(x => [GetCompletionStatusGroupName(x)]),
            PropertyTypeEnum.LastReadTime => CreateGrouper(x => [GetLastReadTimeGroupName(x)]),
            PropertyTypeEnum.Pages => CreateGrouper(x => [GetPagesGroupName(x)]),
            PropertyTypeEnum.PluginVirtualProperty => CreatePluginPropertyGrouper(),
            _ => CreateFallbackGrouper(),
        };
    }

    private static IGroupSorter<ComicGroup, string> GetGroupSorter(ComicFilterModel.OrderMethodEnum orderMethod,
        ComicFilterModel.FunctionTypeEnum sortingFunction, ComicPropertyModel? sortingProperty,
        IVirtualPropertySorter<IComicModel>? sortingPropertyPluginSorter, IGroupSorter<ComicGroup, string> defaultSorter)
    {
        switch (orderMethod)
        {
            case ComicFilterModel.OrderMethodEnum.Shuffle:
            case ComicFilterModel.OrderMethodEnum.ShuffleStable:
                int IdSelector(ComicGroup x) => HashUtils.GetXxHash64Int(x.GroupName);
                return new RandomSorter<ComicGroup>(orderMethod, IdSelector);
            default:
                break;
        }

        double? PropertyToNumber(ComicModel comic)
        {
            return sortingProperty?.Type switch
            {
                PropertyTypeEnum.Title => comic.Title.Length,
                PropertyTypeEnum.Tag => sortingProperty.Name.Length,
                PropertyTypeEnum.Progress => Math.Max(comic.Progress, 0),
                PropertyTypeEnum.Rating => comic.Rating >= 0 ? comic.Rating * 0.05 : null,
                PropertyTypeEnum.CompletionState => (int)comic.CompletionState,
                PropertyTypeEnum.LastReadTime => comic.LastVisit != DateTimeOffset.MinValue ? comic.LastVisit.ToUnixTimeMilliseconds() : null,
                PropertyTypeEnum.Pages => comic.PageCount > 0 ? comic.PageCount : null,
                PropertyTypeEnum.PluginVirtualProperty => sortingPropertyPluginSorter?.AsNumber(comic),
                _ => null,
            };
        }

        string NumberToString(double value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.##");
        }

        IGroupSorter<ComicGroup, string> CreateSorter<T>(Func<ComicGroup, T> keySelector, Func<T, string>? keyInfoConverter = null, IComparer<T>? keyComparer = null)
        {
            return new BasicGroupSorter<ComicGroup, T>(orderMethod, keySelector, keyInfoConverter, keyComparer);
        }

        return sortingFunction switch
        {
            ComicFilterModel.FunctionTypeEnum.None => defaultSorter,
            ComicFilterModel.FunctionTypeEnum.ItemCount => CreateSorter(x => x.Items.Count, keyInfoConverter: x => x.ToString()),
            ComicFilterModel.FunctionTypeEnum.Max => CreateSorter(x => x.Items.Max(PropertyToNumber) ?? 0, keyInfoConverter: NumberToString),
            ComicFilterModel.FunctionTypeEnum.Min => CreateSorter(x => x.Items.Min(PropertyToNumber) ?? 0, keyInfoConverter: NumberToString),
            ComicFilterModel.FunctionTypeEnum.Sum => CreateSorter(x => x.Items.Sum(PropertyToNumber) ?? 0, keyInfoConverter: NumberToString),
            ComicFilterModel.FunctionTypeEnum.Average => CreateSorter(x => x.Items.Average(PropertyToNumber) ?? 0, keyInfoConverter: NumberToString),
            _ => throw new ArgumentOutOfRangeException(nameof(sortingFunction)),
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

        IEnumerable<PluginContext> plugins = PluginManager.Instance.GetActivePlugins();
        foreach (PluginContext plugin in plugins)
        {
            string pluginName = plugin.Name;
            IEnumerable<IVirtualProperty<IComicModel>> pluginProperties = plugin.GetAllComicVirtualProperties();
            foreach (IVirtualProperty<IComicModel> property in pluginProperties)
            {
                properties.Add(new ComicPropertyModel
                {
                    Type = PropertyTypeEnum.PluginVirtualProperty,
                    Name = $"{pluginName}:{property.Name}",
                    DisplayName = property.DisplayName,
                });
            }
        }

        return properties;
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
            PropertyTypeEnum.PluginVirtualProperty => PROP_TYPE_PLUGIN_VIRTUAL_PROPERTY,
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
            PROP_TYPE_PLUGIN_VIRTUAL_PROPERTY => PropertyTypeEnum.PluginVirtualProperty,
            _ => PropertyTypeEnum.Title,
        };
    }

    public class GroupItem<T>(List<T> items, string name)
    {
        public List<T> Items { get; } = items;
        public string Name { get; } = name;
        public string Description { get; set; } = string.Empty;
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
        PluginVirtualProperty,
    }

    private class JsonModel
    {
        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        [JsonPropertyName("Name")]
        public string? Name { get; set; }
    }

    private class ComicGroup(string groupName, List<ComicModel> items) : IItemGroup<IComicModel>
    {
        public string GroupName { get; } = groupName;
        public IReadOnlyList<ComicModel> Items { get; } = items;

        //
        // IItemGroup<IComicModel> implementation
        //

        string IItemGroup<IComicModel>.Name => GroupName;

        IReadOnlyList<IComicModel> IItemGroup<IComicModel>.Items => Items;
    }

    private class ComicGrouper(IGroupSorter<ComicGroup, string> groupSorter, Func<ComicModel, IEnumerable<string>> groupNameSelector)
    {
        private readonly IGroupSorter<ComicGroup, string> _groupSorter = groupSorter;
        private readonly Func<ComicModel, IEnumerable<string>> _groupNameSelector = groupNameSelector;

        public List<GroupItem<T>> GroupComics<T>(IEnumerable<T> items, Func<T, ComicModel> selector)
        {
            Dictionary<string, List<T>> groupMap = [];
            foreach (T item in items)
            {
                IEnumerable<string> groupNames = _groupNameSelector(selector(item));
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

            List<GroupItem<T>> sorted = _groupSorter.Sort(comicGroups, x => new(x.Name, x.Items.ConvertAll(y => selector(y))), (m, t) =>
            {
                if (!string.IsNullOrEmpty(t))
                {
                    m.Description = $"({t})";
                }
            });

            if (sorted.All(x => string.IsNullOrEmpty(x.Description)))
            {
                sorted.ForEach(x => x.Description = $"({x.Items.Count})");
            }

            return sorted;
        }
    }

    private interface IItemSorter<K>
    {
        List<T> Sort<T>(IEnumerable<T> items, Func<T, K> selector);
    }

    private interface IGroupSorter<K, M>
    {
        List<T> Sort<T>(IEnumerable<T> items, Func<T, K> selector, Action<T, M> keyBinder);
    }

    private class RandomSorter<K>(ComicFilterModel.OrderMethodEnum orderMethod, Func<K, int> idSelector) : IItemSorter<K>, IGroupSorter<K, string>
    {
        protected Func<K, int> IdSelector { get; } = idSelector;

        public List<T> Sort<T>(IEnumerable<T> items, Func<T, K> selector, Action<T, string> keyBinder)
        {
            return Sort(items, selector);
        }

        public List<T> Sort<T>(IEnumerable<T> items, Func<T, K> selector)
        {
            switch (orderMethod)
            {
                case ComicFilterModel.OrderMethodEnum.Shuffle:
                    {
                        int salt = Random.Shared.Next();
                        return [.. items.OrderBy(x => IdSelector(selector(x)) ^ salt)];
                    }
                case ComicFilterModel.OrderMethodEnum.ShuffleStable:
                    {
                        var rng = new Random(AppSettingsModel.Instance.GetModel().ComicShuffleRandomSeed);
                        int salt = rng.Next();
                        return [.. items.OrderBy(x => IdSelector(selector(x)) ^ salt)];
                    }
                default:
                    throw new Exception("Invalid order method for RandomSorter");
            }
        }
    }

    private class BasicItemSorter<A, B>(ComicFilterModel.OrderMethodEnum orderMethod, Func<A, B> keySelector, IComparer<B>? keyComparer = null) : IItemSorter<A>
    {
        protected IComparer<B> KeyComparer { get; } = keyComparer ?? Comparer<B>.Default;
        protected Func<A, B> KeySelector { get; } = keySelector;

        public List<T> Sort<T>(IEnumerable<T> items, Func<T, A> selector)
        {
            return orderMethod switch
            {
                ComicFilterModel.OrderMethodEnum.Ascending => [.. items.OrderBy(x => KeySelector(selector(x)), KeyComparer)],
                ComicFilterModel.OrderMethodEnum.Descending => [.. items.OrderByDescending(x => KeySelector(selector(x)), KeyComparer)],
                _ => throw new Exception("Invalid order method for BasicItemSorter"),
            };
        }
    }

    private class BasicGroupSorter<A, B>(ComicFilterModel.OrderMethodEnum orderMethod, Func<A, B> keySelector, Func<B, string>? keyInfoConverter = null, IComparer<B>? keyComparer = null) : IGroupSorter<A, string>
    {
        protected Func<A, B> KeySelector { get; } = keySelector;
        private Func<B, string> KeyInfoConverter { get; } = keyInfoConverter ?? (_ => string.Empty);
        protected IComparer<B> KeyComparer { get; } = keyComparer ?? Comparer<B>.Default;

        public List<T> Sort<T>(IEnumerable<T> items, Func<T, A> selector, Action<T, string> keyBinder)
        {
            B GroupKeySelector(T item)
            {
                B key = KeySelector(selector(item));
                keyBinder(item, KeyInfoConverter(key));
                return key;
            }

            return orderMethod switch
            {
                ComicFilterModel.OrderMethodEnum.Ascending => [.. items.OrderBy(GroupKeySelector, KeyComparer)],
                ComicFilterModel.OrderMethodEnum.Descending => [.. items.OrderByDescending(GroupKeySelector, KeyComparer)],
                _ => throw new Exception("Invalid order method for BasicGroupSorter"),
            };
        }
    }

    private class PluginPropertyItemSorter(IVirtualPropertySorter<IComicModel> sorter, ComicFilterModel.OrderMethodEnum orderMethod) : IItemSorter<ComicModel>
    {
        public List<T> Sort<T>(IEnumerable<T> items, Func<T, ComicModel> selector)
        {
            ILookup<ComicModel, T> lookup = items.ToLookup(t => selector(t));
            IEnumerable<T> sorted = sorter
                .SortItems(items.Select(x => selector(x)))
                .SelectMany(t => lookup[t]);
            return orderMethod switch
            {
                ComicFilterModel.OrderMethodEnum.Ascending => [.. sorted],
                ComicFilterModel.OrderMethodEnum.Descending => [.. sorted.Reverse()],
                _ => throw new Exception("Invalid order method for PluginPropertyItemSorter"),
            };
        }
    }

    private class PluginPropertyGroupSorter(IVirtualPropertySorter<IComicModel> sorter, ComicFilterModel.OrderMethodEnum orderMethod) : IGroupSorter<ComicGroup, string>
    {
        public List<T> Sort<T>(IEnumerable<T> items, Func<T, ComicGroup> selector, Action<T, string> keyBinder)
        {
            ILookup<string, T> lookup = items.ToLookup(t => selector(t).GroupName);
            IEnumerable<T> sorted = sorter
                .SortGroups(items.Select(x => selector(x)))
                .SelectMany(t => lookup[t.GroupName]);
            return orderMethod switch
            {
                ComicFilterModel.OrderMethodEnum.Ascending => [.. sorted],
                ComicFilterModel.OrderMethodEnum.Descending => [.. sorted.Reverse()],
                _ => throw new Exception("Invalid order method for PluginPropertyGroupSorter"),
            };
        }
    }
}
