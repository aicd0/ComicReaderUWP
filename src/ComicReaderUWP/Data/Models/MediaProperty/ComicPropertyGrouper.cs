// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.AppEnvironment;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.SDK.Plugins.Comic;
using ComicReaderUWP.SDK.Plugins.Property;

using PropertyTypeEnum = ComicReaderUWP.Data.Models.MediaProperty.ComicPropertyModel.PropertyTypeEnum;

namespace ComicReaderUWP.Data.Models.MediaProperty;

internal static class ComicPropertyGrouper
{
    private const string TAG = nameof(ComicPropertyGrouper);

    public static List<GroupItem<T>> GroupComics<T>(this ComicPropertyModel property,
        IEnumerable<T> items,
        Func<T, ComicModel> selector,
        ComicFilterModel.OrderMethodEnum orderMethod,
        ComicFilterModel.FunctionTypeEnum sortingFunction,
        ComicPropertyModel? sortingProperty,
        bool mergeSingleItemGroups)
    {
        IEnumerable<T> preSorted = items.OrderBy(x => StringUtils.SmartFileNameKeySelector(selector(x).Title ?? StringResourceProvider.Instance.Untitled), StringUtils.SmartFileNameComparer);
        IVirtualPropertySorter<IComicModel>? pluginSorter = property.PluginProperty?.CreateSorter(preSorted.Select(selector));
        IVirtualPropertySorter<IComicModel>? sortingPropertyPluginSorter = sortingProperty?.PluginProperty?.CreateSorter(preSorted.Select(selector));
        ComicGrouper grouper = GetComicGrouper(property, orderMethod, pluginSorter, sortingFunction, sortingProperty, sortingPropertyPluginSorter);
        return grouper.GroupComics(preSorted, selector, mergeSingleItemGroups);
    }

    private static ComicGrouper GetComicGrouper(ComicPropertyModel property, ComicFilterModel.OrderMethodEnum orderMethod, IVirtualPropertySorter<IComicModel>? pluginSorter,
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
            if (!comic.Tags.TryGetValue(property.Name, out ComicTagCategory? category))
            {
                string name = StringResourceProvider.Instance.Ungrouped;
                return (string[])[name];
            }

            return (string[])[.. category.Tags];
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
            return CompletionStatusService.EnumToString(comic.CompletionStatus, fallback: StringResourceProvider.Instance.Ungrouped);
        }

        int GetCompletionStatusGroupSortingKey(ComicModel comic)
        {
            return CompletionStatusService.EnumToComparable(comic.CompletionStatus);
        }

        string GetReleaseDateVariantGroupName(ComicModel comic)
        {
            if (comic.ReleaseDate is not { } releaseDate)
            {
                return StringResourceProvider.Instance.Ungrouped;
            }

            return GetDateGroupName(releaseDate, property.Type);
        }

        string GetLastReadTimeVariantGroupName(ComicModel comic)
        {
            DateTimeOffset lastReadTime = comic.LastVisit;
            if (lastReadTime == DateTimeOffset.MinValue)
            {
                return StringResourceProvider.Instance.Ungrouped;
            }

            return GetDateGroupName(lastReadTime.DateTime, property.Type);
        }

        long GetReleaseDateVariantGroupSortingKey(ComicModel comic)
        {
            return comic.ReleaseDate is { } releaseDate ? GetPeriodStart(releaseDate, property.Type).Ticks : long.MinValue;
        }

        long GetLastReadTimeVariantGroupSortingKey(ComicModel comic)
        {
            DateTimeOffset lastReadTime = comic.LastVisit;
            return lastReadTime != DateTimeOffset.MinValue ? GetPeriodStart(lastReadTime.DateTime, property.Type).Ticks : long.MinValue;
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

            return property.Type switch
            {
                PropertyTypeEnum.Title => CreateSorter(x => StringUtils.SmartFileNameKeySelector(x.GroupName), keyComparer: StringUtils.SmartFileNameComparer),
                PropertyTypeEnum.Progress => CreateSorter(x => x.Items[0].Progress),
                PropertyTypeEnum.Tag => CreateSorter(x => StringUtils.SmartFileNameKeySelector(x.GroupName), keyComparer: StringUtils.SmartFileNameComparer),
                PropertyTypeEnum.Rating => CreateSorter(x => x.Items[0].Rating),
                PropertyTypeEnum.CompletionStatus => CreateSorter(x => GetCompletionStatusGroupSortingKey(x.Items[0])),
                PropertyTypeEnum.LastReadTimeByYear or PropertyTypeEnum.LastReadTimeBySeason or PropertyTypeEnum.LastReadTimeByMonth => CreateSorter(x => GetLastReadTimeVariantGroupSortingKey(x.Items[0])),
                PropertyTypeEnum.Pages => CreateSorter(x => x.Items[0].PageCount),
                PropertyTypeEnum.ReleaseDateByYear or PropertyTypeEnum.ReleaseDateBySeason or PropertyTypeEnum.ReleaseDateByMonth => CreateSorter(x => GetReleaseDateVariantGroupSortingKey(x.Items[0])),
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
            return CreateGrouper(x => (string[])[new(StringResourceProvider.Instance.Ungrouped)]);
        }

        ComicGrouper CreatePluginPropertyGrouper()
        {
            if (pluginSorter is null)
            {
                return CreateFallbackGrouper();
            }

            return CreateGrouper(pluginSorter.GetGroupNames);
        }

        return property.Type switch
        {
            PropertyTypeEnum.Title => CreateGrouper(x => (string[])[GetTitleGroupName(x)]),
            PropertyTypeEnum.Progress => CreateGrouper(x => (string[])[GetProgressGroupName(x)]),
            PropertyTypeEnum.Tag => CreateGrouper(GetTagGroupNames),
            PropertyTypeEnum.Rating => CreateGrouper(x => (string[])[GetRatingGroupName(x)]),
            PropertyTypeEnum.CompletionStatus => CreateGrouper(x => (string[])[GetCompletionStatusGroupName(x)]),
            PropertyTypeEnum.LastReadTimeByYear or PropertyTypeEnum.LastReadTimeBySeason or PropertyTypeEnum.LastReadTimeByMonth => CreateGrouper(x => (string[])[GetLastReadTimeVariantGroupName(x)]),
            PropertyTypeEnum.Pages => CreateGrouper(x => (string[])[GetPagesGroupName(x)]),
            PropertyTypeEnum.ReleaseDateByYear or PropertyTypeEnum.ReleaseDateBySeason or PropertyTypeEnum.ReleaseDateByMonth => CreateGrouper(x => (string[])[GetReleaseDateVariantGroupName(x)]),
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
                PropertyTypeEnum.CompletionStatus => (int)comic.CompletionStatus,
                PropertyTypeEnum.LastReadTime => comic.LastVisit != DateTimeOffset.MinValue ? comic.LastVisit.ToUnixTimeMilliseconds() : null,
                PropertyTypeEnum.Pages => comic.PageCount > 0 ? comic.PageCount : null,
                PropertyTypeEnum.ReleaseDate => comic.ReleaseDate is { } releaseDate ? (releaseDate - DateTime.UnixEpoch).TotalMilliseconds : null,
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

    private static DateTime GetPeriodStart(DateTime value, PropertyTypeEnum type)
    {
        return type switch
        {
            PropertyTypeEnum.LastReadTimeBySeason or PropertyTypeEnum.ReleaseDateBySeason => new DateTime(value.Year, (value.Month - 1) / 3 * 3 + 1, 1),
            PropertyTypeEnum.LastReadTimeByMonth or PropertyTypeEnum.ReleaseDateByMonth => new DateTime(value.Year, value.Month, 1),
            _ => new DateTime(value.Year, 1, 1),
        };
    }

    private static string GetDateGroupName(DateTime value, PropertyTypeEnum type)
    {
        DateTime periodStart = GetPeriodStart(value, type);
        if (type is PropertyTypeEnum.LastReadTimeBySeason or PropertyTypeEnum.ReleaseDateBySeason)
        {
            int quarter = (periodStart.Month - 1) / 3 + 1;
            return string.Format(CultureInfo.InvariantCulture, "{0} Q{1}", periodStart.Year, quarter);
        }

        if (type is PropertyTypeEnum.LastReadTimeByMonth or PropertyTypeEnum.ReleaseDateByMonth)
        {
            return periodStart.ToString("Y", EnvironmentProvider.Instance.GetCurrentAppLanguageInfo());
        }

        return periodStart.Year.ToString(CultureInfo.InvariantCulture);
    }
}

internal class GroupItem<T>(List<T> items, string name)
{
    public List<T> Items { get; } = items;
    public string Name { get; } = name;
    public string Description { get; set; } = string.Empty;
}

internal class ComicGroup(string groupName, List<ComicModel> items) : IItemGroup<IComicModel>
{
    public string GroupName { get; } = groupName;
    public IReadOnlyList<ComicModel> Items { get; } = items;

    //
    // IItemGroup<IComicModel> implementation
    //

    string IItemGroup<IComicModel>.Name => GroupName;

    IReadOnlyList<IComicModel> IItemGroup<IComicModel>.Items => Items;
}

internal class ComicGrouper(IGroupSorter<ComicGroup, string> groupSorter, Func<ComicModel, IEnumerable<string>> groupNameSelector)
{
    private readonly IGroupSorter<ComicGroup, string> _groupSorter = groupSorter;
    private readonly Func<ComicModel, IEnumerable<string>> _groupNameSelector = groupNameSelector;

    public List<GroupItem<T>> GroupComics<T>(IEnumerable<T> items, Func<T, ComicModel> selector, bool mergeSingleItemGroups)
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

        List<GroupItem<T>> mediaGroups = [];
        foreach (KeyValuePair<string, List<T>> p in groupMap)
        {
            mediaGroups.Add(new(p.Value, p.Key));
        }

        if (mergeSingleItemGroups)
        {
            var singleItemGroups = mediaGroups.Where(x => x.Items.Count == 1).ToList();
            if (singleItemGroups.Count >= 2)
            {
                var mergedGroup = singleItemGroups.SelectMany(x => x.Items).ToList();
                mediaGroups.RemoveAll(x => x.Items.Count == 1);
                mediaGroups.Add(new GroupItem<T>(mergedGroup, StringResourceProvider.Instance.Miscellaneous));
            }
        }

        List<GroupItem<T>> sorted = _groupSorter.Sort(mediaGroups, x => new(x.Name, x.Items.ConvertAll(y => selector(y))), (m, t) =>
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

internal class PluginPropertyGroupSorter(IVirtualPropertySorter<IComicModel> sorter, ComicFilterModel.OrderMethodEnum orderMethod) : IGroupSorter<ComicGroup, string>
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
