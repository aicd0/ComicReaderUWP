// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Threading;
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

    public void OnStop()
    {
        Logger.RemoveListener(_logListener);
    }

    public void SetLogStarted(bool started)
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
    }

    public void SetLogVisibility(bool visible)
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
                List<LogTag?> consoleWhitelist = DebugSwitchModel.Instance.ConsoleWhitelist;
                if (!consoleWhitelist.Any(t => t is null || t.ContainsAny(tag)))
                {
                    return;
                }
            }

            viewModel.AppendLog(message);
        }
    }
}
