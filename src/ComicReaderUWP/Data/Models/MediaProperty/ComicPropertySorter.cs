// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

using ComicReaderUWP.Common.Localization;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Models.Comic;
using ComicReaderUWP.Data.Models.Misc;
using ComicReaderUWP.SDK.Plugins.Comic;
using ComicReaderUWP.SDK.Plugins.Property;

using PropertyTypeEnum = ComicReaderUWP.Data.Models.MediaProperty.ComicPropertyModel.PropertyTypeEnum;

namespace ComicReaderUWP.Data.Models.MediaProperty;

internal static class ComicPropertySorter
{
    private const string TAG = nameof(ComicPropertySorter);

    public static List<T> SortComics<T>(this ComicPropertyModel property, IEnumerable<T> items, Func<T, ComicModel> selector, ComicFilterModel.OrderMethodEnum orderMethod)
    {
        IEnumerable<T> preSorted = items.OrderBy(x => StringUtils.SmartFileNameKeySelector(selector(x).Title ?? StringResourceProvider.Instance.Untitled), StringUtils.SmartFileNameComparer);
        IVirtualPropertySorter<IComicModel>? pluginSorter = property.PluginProperty?.CreateSorter(preSorted.Select(selector));
        IItemSorter<ComicModel> sorter = GetComicItemSorter(property, orderMethod, pluginSorter);
        return sorter.Sort(preSorted, selector);
    }

    private static IItemSorter<ComicModel> GetComicItemSorter(ComicPropertyModel property, ComicFilterModel.OrderMethodEnum orderMethod, IVirtualPropertySorter<IComicModel>? pluginSorter)
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
            if (!comic.Tags.TryGetValue(property.Name, out ComicTagCategory? category))
            {
                return string.Empty;
            }

            List<string> tags = [.. category.Tags];
            tags.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join(' ', tags);
        }

        int CompletionStateToComparable(Comic.CompletionStatusEnum status)
        {
            return CompletionStatusService.EnumToComparable(status);
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

        return property.Type switch
        {
            PropertyTypeEnum.Title => CreateSorter(x => StringUtils.SmartFileNameKeySelector(x.Title ?? StringResourceProvider.Instance.Untitled), keyComparer: StringUtils.SmartFileNameComparer),
            PropertyTypeEnum.Progress => CreateSorter(x => x.Progress),
            PropertyTypeEnum.Tag => CreateSorter(x => StringUtils.SmartFileNameKeySelector(GetConcatenatedTag(x)), keyComparer: StringUtils.SmartFileNameComparer),
            PropertyTypeEnum.Rating => CreateSorter(x => x.Rating),
            PropertyTypeEnum.CompletionStatus => CreateSorter(x => CompletionStateToComparable(x.CompletionStatus)),
            PropertyTypeEnum.LastReadTime => CreateSorter(x => x.LastVisit.Ticks),
            PropertyTypeEnum.Pages => CreateSorter(x => x.PageCount),
            PropertyTypeEnum.ReleaseDate => CreateSorter(x => x.ReleaseDate),
            PropertyTypeEnum.PluginVirtualProperty => CreatePluginPropertySorter(),
            _ => CreateFallbackSorter(),
        };
    }
}

internal class PluginPropertyItemSorter(IVirtualPropertySorter<IComicModel> sorter, ComicFilterModel.OrderMethodEnum orderMethod) : IItemSorter<ComicModel>
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
