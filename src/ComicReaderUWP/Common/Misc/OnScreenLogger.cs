// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.SDK.Common.DebugTools;
using ComicReaderUWP.SDK.Common.Lifecycle;
using ComicReaderUWP.SDK.Common.Utils;

namespace ComicReaderUWP.Common.Misc;

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
        SetLogVisibility(AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).GetValueOrDefault(KVNames.KV_KEY_APP_LOG_VISIBLE, false));
        SetLogStarted(AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).GetValueOrDefault(KVNames.KV_KEY_APP_LOG_STARTED, true));
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
        AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).Set(KVNames.KV_KEY_APP_LOG_STARTED, started);
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
        AppDB.AppKV.GetCollection(KVNames.KV_LIB_APP).Set(KVNames.KV_KEY_APP_LOG_VISIBLE, visible);
        _visible.Emit(visible);
    }
}
