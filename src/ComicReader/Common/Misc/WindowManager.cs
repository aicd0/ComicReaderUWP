// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using ComicReader.Common.Constants;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Lifecycle;
using ComicReader.SDK.Common.Threading;
using ComicReader.SDK.Common.Utils;
using ComicReader.SDK.Database.KV;
using ComicReader.Views.AppWindows.Main;
using ComicReader.Views.Pages.Main;

namespace ComicReader.Common.Misc;

class WindowManager
{
    private const string TAG = nameof(WindowManager);

    private int _highestWindowId = 0;
    private readonly ConcurrentDictionary<int, WindowWrapper> _windows = [];
    private bool _saveWindowStatusScheduled = false;
    private bool _windowStatusLocked = false;

    public int RegisterWindow(MainWindow window)
    {
        int windowId = Interlocked.Increment(ref _highestWindowId);
        WindowWrapper wrapper = new(window);
        bool success = _windows.TryAdd(windowId, wrapper);
        Logger.Assert(success, "B62A8795DA9036E2");
        return windowId;
    }

    public void UnregisterWindow(int windowId)
    {
        bool success = _windows.TryRemove(windowId, out _);
        Logger.Assert(success, "1A3BA06AD5A4351E");
    }

    public MainWindow? GetAnyWindow()
    {
        foreach (WindowWrapper wrapper in _windows.Values)
        {
            return wrapper.Window;
        }

        return null;
    }

    public MainWindow? GetActiveWindow()
    {
        foreach (WindowWrapper wrapper in _windows.Values)
        {
            if (wrapper.Window.IsActive)
            {
                return wrapper.Window;
            }
        }

        return null;
    }

    public MainWindow? GetWindow(int windowId)
    {
        if (_windows.TryGetValue(windowId, out WindowWrapper? wrapper))
        {
            return wrapper.Window;
        }

        return null;
    }

    public IEventBus GetEventBus(int windowId)
    {
        if (_windows.TryGetValue(windowId, out WindowWrapper? wrapper))
        {
            return wrapper.EventBus;
        }

        Logger.F(TAG, $"Unable to get desired event bus, window ID {windowId} not found.");
        return EmptyEventBus.Instance;
    }

    public Dictionary<int, string> GetAllWindowInfo()
    {
        Dictionary<int, string> result = [];
        foreach (KeyValuePair<int, WindowWrapper> pair in _windows)
        {
            MainPage.ITabInfo? tabInfo = pair.Value.Window.CurrentTab;
            if (tabInfo is not null)
            {
                result[pair.Key] = tabInfo.Title;
            }
        }

        return result;
    }

    public void ScheduleSaveWindowStatus()
    {
        if (_saveWindowStatusScheduled || _windowStatusLocked)
        {
            return;
        }

        _saveWindowStatusScheduled = true;
        CoroutineUtils.Start(async () =>
        {
            await Task.Delay(500);
            _saveWindowStatusScheduled = false;
            if (_windowStatusLocked)
            {
                return;
            }

            SaveWindowStatus();
        });
    }

    public void LockWindowStatus()
    {
        if (_windowStatusLocked)
        {
            return;
        }

        _windowStatusLocked = true;
        SaveWindowStatus();
    }

    public void RestoreWindowStatus()
    {
        WindowStatusModel? model = null;
        string? serialized = KVStore.App.GetCollection(DatabaseEntry.KV_LIB_APP).GetValue<string>(DatabaseEntry.KV_KEY_APP_WINDOW_STATUS);
        if (!string.IsNullOrEmpty(serialized))
        {
            try
            {
                model = JsonSerializer.Deserialize<WindowStatusModel>(serialized);
            }
            catch (JsonException ex)
            {
                Logger.E(TAG, "Failed to deserialize window status model.", ex);
            }
        }

        if (model is null || model.Windows.Count == 0)
        {
            MainWindow.Open();
            return;
        }

        foreach (MainWindow.WindowStatusModel windowStatus in model.Windows)
        {
            MainWindow.Open(windowStatus);
        }
    }

    private void SaveWindowStatus()
    {
        CoroutineUtils.Start(async () =>
        {
            WindowStatusModel model = new()
            {
                Windows = []
            };

            await MainThreadUtils.RunInMainThread(() =>
            {
                foreach (WindowWrapper wrapper in _windows.Values)
                {
                    MainWindow.WindowStatusModel? windowStatus = wrapper.Window.GetWindowStatus();
                    if (windowStatus is not null)
                    {
                        model.Windows.Add(windowStatus);
                    }
                }
            });

            string serialized = JsonSerializer.Serialize(model);
            KVStore.App.GetCollection(DatabaseEntry.KV_LIB_APP).Set(DatabaseEntry.KV_KEY_APP_WINDOW_STATUS, serialized);
        });
    }

    private class WindowWrapper(MainWindow window)
    {
        public MainWindow Window { get; set; } = window;
        public EventBus EventBus { get; } = new();
    }

    private class WindowStatusModel
    {
        [JsonPropertyName("Windows")]
        public required List<MainWindow.WindowStatusModel> Windows { get; init; }
    }
}
