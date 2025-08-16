// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.ComponentModel;

using ComicReader.Common.Constants;
using ComicReader.Common.Threading;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.KVStorage;
using ComicReader.ViewModels;

namespace ComicReader.Views.Pages.Main;

internal partial class MainPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<LogItemViewModel> LogItems { get; } = [];

    private bool _isFullscreen = false;
    public bool IsFullscreen
    {
        get => _isFullscreen;
        set
        {
            _isFullscreen = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFullscreen)));
        }
    }

    private bool _isLogVisible = false;
    public bool IsLogVisible
    {
        get => _isLogVisible;
        set
        {
            _isLogVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLogVisible)));
        }
    }

    private readonly LogListener _logListener;
    private bool _logStarted = false;

    public MainPageViewModel()
    {
        _logListener = new LogListener(this);
    }

    public void OnStart()
    {
        SetLogVisibility(KVDatabase.Default.GetBoolean(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_LOG_VISIBLE, false));
        SetLogStarted(KVDatabase.Default.GetBoolean(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_LOG_STARTED, true));
    }

    public void OnStop()
    {
        Logger.RemoveListener(_logListener);
    }

    public void StartOrPauseLog()
    {
        SetLogStarted(!_logStarted);
    }

    public void ToggleLogVisibility()
    {
        SetLogVisibility(!_isLogVisible);
    }

    private void SetLogStarted(bool started)
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
        if (started)
        {
            Logger.AddListener(_logListener);
        }
        else
        {
            Logger.RemoveListener(_logListener);
        }

        KVDatabase.Default.SetBoolean(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_LOG_STARTED, started);
    }

    private void SetLogVisibility(bool visible)
    {
        if (visible && !DebugUtils.DeveloperMode)
        {
            return;
        }

        if (_isLogVisible == visible)
        {
            return;
        }

        IsLogVisible = visible;
        KVDatabase.Default.SetBoolean(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_LOG_VISIBLE, visible);
    }

    private void AppendLog(string message)
    {
        MainThreadUtils.RunInMainThread(() =>
        {
            LogItemViewModel item = new()
            {
                Text = message,
            };

            LogItems.Insert(0, item);
            while (LogItems.Count > 100)
            {
                LogItems.RemoveAt(LogItems.Count - 1);
            }
        });
    }

    private class LogListener(MainPageViewModel viewModel) : Logger.ILogListener
    {
        void Logger.ILogListener.OnLog(int level, LogTag tag, string message)
        {
            if (level <= 2)
            {
                LogTag? consoleWhitelist = DebugSwitchModel.Instance.ConsoleWhitelist;
                if (consoleWhitelist != null && !consoleWhitelist.ContainsAny(tag))
                {
                    return;
                }
            }

            viewModel.AppendLog(message);
        }
    }
}
