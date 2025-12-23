// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.Constants;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Lifecycle;
using ComicReader.SDK.Common.Utils;
using ComicReader.SDK.Database.KV;

namespace ComicReader.Common;

internal static class OnscreenLogger
{
    private static readonly MutableLiveData<bool> _started = new(false);
    public static ILiveData<bool> Started => _started;

    private static readonly MutableLiveData<bool> _visible = new(false);
    public static ILiveData<bool> Visible => _visible;

    private static bool _initialize = false;
    private static bool _logStarted = false;
    private static bool _logVisible = false;

    public static void Initialize()
    {
        if (_initialize)
        {
            return;
        }

        _initialize = true;
        SetLogVisibility(KVStore.App.GetCollection(DatabaseEntry.KV_LIB_APP).GetValueOrDefault(DatabaseEntry.KV_KEY_APP_LOG_VISIBLE, false));
        SetLogStarted(KVStore.App.GetCollection(DatabaseEntry.KV_LIB_APP).GetValueOrDefault(DatabaseEntry.KV_KEY_APP_LOG_STARTED, true));
    }

    public static void StartOrPause()
    {
        Initialize();
        SetLogStarted(!_logStarted);
    }

    public static void ShowOrHide()
    {
        Initialize();
        SetLogVisibility(!_logVisible);
    }

    private static void SetLogStarted(bool started)
    {
        if (started && !DebugUtils.DeveloperMode)
        {
            return;
        }

        if (started == _logStarted)
        {
            return;
        }

        _logStarted = started;
        KVStore.App.GetCollection(DatabaseEntry.KV_LIB_APP).Set(DatabaseEntry.KV_KEY_APP_LOG_STARTED, started);
        _started.Emit(started);
    }

    private static void SetLogVisibility(bool visible)
    {
        if (visible && !DebugUtils.DeveloperMode)
        {
            return;
        }

        if (_logVisible == visible)
        {
            return;
        }

        _logVisible = visible;
        KVStore.App.GetCollection(DatabaseEntry.KV_LIB_APP).Set(DatabaseEntry.KV_KEY_APP_LOG_VISIBLE, visible);
        _visible.Emit(visible);
    }
}
