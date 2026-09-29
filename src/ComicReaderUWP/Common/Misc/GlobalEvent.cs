// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Lifecycle.Utils;
using ComicReaderUWP.Core.Common.Utils;

namespace ComicReaderUWP.Common.Misc;

internal class GlobalEvent
{
    private static readonly Lazy<GlobalEvent> _lazyInstance = new(() => new GlobalEvent());

    public static GlobalEvent Instance => _lazyInstance.Value;

    private GlobalEvent() { }

    public MutableLiveDataWithDelay<IEnumerable<long>> CollectionUpdated = new(
        EventBus.Default.With("CollectionUpdated", () => new MutableLiveData<IEnumerable<long>>() { Lossless = true }),
        1000,
        delay: 100,
        mergeFunc: static (a, b) => [.. a.Union(b)]);
    public MutableLiveDataWithDelay<IEnumerable<long>> ComicUpdated = new(
        EventBus.Default.With("ComicUpdated", () => new MutableLiveData<IEnumerable<long>>() { Lossless = true }),
        1000,
        delay: 100,
        mergeFunc: static (a, b) => [.. a.Union(b)]);
    public MutableLiveDataWithDelay<object> FilterUpdated = new(EventBus.Default.With("FilterUpdated"), 1000, delay: 100);
    public MutableLiveDataWithDelay<object> FavoriteUpdated = new(EventBus.Default.With("FavoriteUpdated"), 1000, delay: 100);
    public MutableLiveDataWithDelay<object> HistoryUpdated = new(EventBus.Default.With("HistoryUpdated"), 1000, delay: 100);
    public MutableLiveDataWithDelay<object> TagInfoUpdated = new(EventBus.Default.With("TagInfoUpdated"), 1000, delay: 100);
}
