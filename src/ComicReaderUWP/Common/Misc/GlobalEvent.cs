// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReaderUWP.Core.Common.Lifecycle;
using ComicReaderUWP.Core.Common.Lifecycle.Utils;

namespace ComicReaderUWP.Common.Misc;

internal class GlobalEvent
{
    private static readonly Lazy<GlobalEvent> _lazyInstance = new(() => new GlobalEvent());

    public static GlobalEvent Instance => _lazyInstance.Value;

    private GlobalEvent() { }

    public IMutableLiveData<object> ComicUpdated = new MutableLiveDataWithMinInterval<object>(EventBus.Default.With("ComicUpdated"), 1000, delay: 100);
    public IMutableLiveData<object> FilterUpdated = new MutableLiveDataWithMinInterval<object>(EventBus.Default.With("FilterUpdated"), 1000, delay: 100);
    public IMutableLiveData<object> FavoriteUpdated = new MutableLiveDataWithMinInterval<object>(EventBus.Default.With("FavoriteUpdated"), 1000, delay: 100);
    public IMutableLiveData<object> HistoryUpdated = new MutableLiveDataWithMinInterval<object>(EventBus.Default.With("HistoryUpdated"), 1000, delay: 100);
    public IMutableLiveData<object> TagInfoUpdated = new MutableLiveDataWithMinInterval<object>(EventBus.Default.With("TagInfoUpdated"), 1000, delay: 100);
}
