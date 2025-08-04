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
    private string _logType = string.Empty;

    public MainPageViewModel()
    {
        _logListener = new LogListener(this);
    }

    public void OnStart()
    {
        string logType = KVDatabase.Default.GetString(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_LOG_TYPE, "");
        SetLogType(logType);
    }

    public void OnStop()
    {
        Logger.RemoveListener(_logListener);
    }

    public void NextLogType()
    {
        SetLogType(IsLogVisible ? "" : "default");
    }

    private void SetLogType(string logType)
    {
        bool isVisible = logType switch
        {
            "default" => true,
            _ => false,
        };

        if (isVisible && !DebugUtils.DeveloperMode)
        {
            return;
        }

        if (_logType == logType)
        {
            return;
        }

        if (IsLogVisible != isVisible)
        {
            IsLogVisible = isVisible;
            if (isVisible)
            {
                Logger.AddListener(_logListener);
            }
            else
            {
                Logger.RemoveListener(_logListener);
            }
        }

        _logType = logType;
        KVDatabase.Default.SetString(DatabaseEntry.KV_LIB_APP, DatabaseEntry.KV_KEY_APP_LOG_TYPE, logType);
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
        void Logger.ILogListener.OnLog(string message)
        {
            viewModel.AppendLog(message);
        }
    }
}
