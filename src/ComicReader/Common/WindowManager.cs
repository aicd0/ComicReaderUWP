// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Threading;

using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Lifecycle;
using ComicReader.Views.AppWindows.Main;

namespace ComicReader.Common;

class WindowManager
{
    private const string TAG = nameof(WindowManager);

    private int _nextWindowId = 0;
    private readonly ConcurrentDictionary<int, WindowWrapper> _windows = [];

    public int RegisterWindow(MainWindow window)
    {
        int windowId = Interlocked.Increment(ref _nextWindowId);
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

        Logger.AssertNotReachHere("6046D73C153C55AB");
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

    private class WindowWrapper(MainWindow window)
    {
        public MainWindow Window { get; set; } = window;
        public EventBus EventBus { get; } = new();
    }
}
