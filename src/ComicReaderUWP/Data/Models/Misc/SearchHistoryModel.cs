// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.SDK.Models;

namespace ComicReaderUWP.Data.Models.Misc;

internal class SearchHistoryModel
{
    private const int MAX_HISTORY = 1024;
    private const int CLEAN_THRESHOLD = 128;

    private static readonly ConcurrentDictionary<string, SearchHistoryModel> _cache = [];

    public static SearchHistoryModel Get(string key)
    {
        key = Convert.ToHexString(HashUtils.GetXxHash64(key));
        if (_cache.TryGetValue(key, out SearchHistoryModel? model))
        {
            return model;
        }

        model = new(key);
        if (_cache.TryAdd(key, model))
        {
            return model;
        }

        return _cache[key];
    }

    private readonly Lazy<IRegistryKey> _registryKey;

    private SearchHistoryModel(string key)
    {
        _registryKey = new(() =>
        {
            return AppDB.MainRegistry.CreateKey($"{RegistryNames.SEARCH_HISTORY}/{key}");
        });
    }

    public IEnumerable<string> Search(IEnumerable<string> keywords, int maxCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount, nameof(maxCount));

        List<string> internalKeywords = [];
        foreach (string keyword in keywords)
        {
            internalKeywords.Add(keyword.ToLowerInvariant());
        }

        var pq = new PriorityQueue<string, long>();
        foreach (string key in _registryKey.Value.Keys)
        {
            if (!_registryKey.Value.TryGet(key, out long searchTime))
            {
                continue;
            }

            if (!Match(internalKeywords, key.ToLowerInvariant()))
            {
                continue;
            }

            pq.Enqueue(key, searchTime);
            if (pq.Count > maxCount)
            {
                pq.Dequeue();
            }
        }

        return pq.UnorderedItems
            .OrderByDescending(x => x.Priority)
            .Select(i => i.Element);
    }

    public void Save(string searchText)
    {
        if (searchText.Length > 128)
        {
            return;
        }

        long time = DateTimeOffset.UtcNow.Ticks;
        _registryKey.Value.Set(searchText, time);

        // Cleanup
        if (_registryKey.Value.Count > MAX_HISTORY + CLEAN_THRESHOLD)
        {
            var pq = new PriorityQueue<string, long>(Comparer<long>.Create((a, b) => b.CompareTo(a)));
            foreach (string key in _registryKey.Value.Keys)
            {
                if (!_registryKey.Value.TryGet(key, out long searchTime))
                {
                    continue;
                }

                pq.Enqueue(key, searchTime);
                if (pq.Count > CLEAN_THRESHOLD + 1)
                {
                    pq.Dequeue();
                }
            }

            IEnumerable<string> clearingKeys = pq.UnorderedItems
                .Select(x => x.Element);
            foreach (string key in clearingKeys)
            {
                _registryKey.Value.Remove(key);
            }
        }
    }

    private static bool Match(List<string> keywords, string searchText)
    {
        return keywords.Count == 0 || StringUtils.FastMatch(keywords, searchText) > 0;
    }
}
