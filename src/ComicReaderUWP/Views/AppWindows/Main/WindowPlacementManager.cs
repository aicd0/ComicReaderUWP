// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Drawing;
using System.Text.Json.Serialization;

using ComicReaderUWP.Core.Common.DebugTools;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ComicReaderUWP.Views.AppWindows.Main;

internal class WindowPlacementManager(MainWindow window)
{
    private const string TAG = nameof(WindowPlacementManager);

    private readonly MainWindow _window = window;
    private SavedWindowState? _frozenWindowPlacement;

    public SavedWindowState? GetWindowPlacement()
    {
        if (_frozenWindowPlacement is not null)
        {
            return _frozenWindowPlacement;
        }

        var hWnd = new HWND(_window.WindowHandle);

        if (!TryGetNativeWindowPlacement(hWnd, out WINDOWPLACEMENT placement))
        {
            Logger.E(TAG, "Failed to get window placement");
            return null;
        }

        bool gotFrameRect;
        RECT currentRect = default;
        unsafe
        {
            int hr = PInvoke.DwmGetWindowAttribute(
                hWnd,
                Windows.Win32.Graphics.Dwm.DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS,
                &currentRect,
                (uint)sizeof(RECT));
            gotFrameRect = hr >= 0;
        }

        if (!gotFrameRect)
        {
            PInvoke.GetWindowRect(hWnd, out currentRect);
        }

        if (!IsValidPlacement(placement, currentRect))
        {
            Logger.I(TAG, "Invalid window placement, not saving");
            return null;
        }

        var state = new SavedWindowState
        {
            Version = SavedWindowState.CURRENT_VERSION,
            Placement = WindowPlacementDto.FromNative(placement),
            CurrentRect = RectDto.FromNative(currentRect),
            MonitorName = GetCurrentMonitorName(hWnd),
        };
        return state;
    }

    public void RestoreWindowPlacement(SavedWindowState state)
    {
        if (state.Version < SavedWindowState.CURRENT_VERSION)
        {
            return;
        }

        var hWnd = new HWND(_window.WindowHandle);

        WINDOWPLACEMENT windowPlacement = state.Placement.ToNative();
        unsafe
        {
            windowPlacement.length = (uint)sizeof(WINDOWPLACEMENT);
        }

        RECT currentRect = state.CurrentRect.ToNative();

        // Check if the monitor still exists, and adjust if necessary
        if (!string.IsNullOrEmpty(state.MonitorName))
        {
            currentRect = AdjustRectToAvailableMonitor(currentRect, state.MonitorName);
        }

        if (!IsValidPlacement(windowPlacement, currentRect))
        {
            Logger.E(TAG, "Invalid window placement, not restoring");
            return;
        }

        PInvoke.SetWindowPlacement(hWnd, in windowPlacement);
        var hWndTop = new HWND(0);
        PInvoke.SetWindowPos(hWnd, hWndTop, currentRect.left, currentRect.top, currentRect.Width, currentRect.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER |
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
    }

    public void FreezeWindowPlacement()
    {
        _frozenWindowPlacement = GetWindowPlacement();
    }

    public void UnfreezeWindowPlacement()
    {
        _frozenWindowPlacement = null;
    }

    private static string GetCurrentMonitorName(HWND hWnd)
    {
        HMONITOR hMonitor = PInvoke.MonitorFromWindow(hWnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);

        unsafe
        {
            var monitorInfo = new MONITORINFOEXW
            {
                monitorInfo =
                {
                    cbSize = (uint)sizeof(MONITORINFOEXW),
                },
                szDevice = default,
            };

            if (PInvoke.GetMonitorInfo(hMonitor, (MONITORINFO*)&monitorInfo))
            {
                return monitorInfo.szDevice.ToString();
            }
        }

        return string.Empty;
    }

    private static RECT AdjustRectToAvailableMonitor(RECT rect, string monitorName)
    {
        HMONITOR hMonitor = PInvoke.MonitorFromRect(in rect, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONULL);

        // If monitor exists and matches the saved one, no adjustment needed
        if (hMonitor != 0)
        {
            unsafe
            {
                var monitorInfo = new MONITORINFOEXW
                {
                    monitorInfo =
                    {
                        cbSize = (uint)sizeof(MONITORINFOEXW),
                    },
                    szDevice = default,
                };

                if (PInvoke.GetMonitorInfo(hMonitor, (MONITORINFO*)&monitorInfo) &&
                    monitorInfo.szDevice.ToString() == monitorName)
                {
                    return rect;
                }
            }
        }

        // Monitor doesn't exist or doesn't match, use primary monitor
        hMonitor = PInvoke.MonitorFromPoint(new Point(0, 0),
            MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);

        unsafe
        {
            var primaryMonitorInfo = new MONITORINFOEXW
            {
                monitorInfo =
                {
                    cbSize = (uint)sizeof(MONITORINFOEXW),
                },
                szDevice = default,
            };

            if (PInvoke.GetMonitorInfo(hMonitor, (MONITORINFO*)&primaryMonitorInfo))
            {
                RECT monitorRect = primaryMonitorInfo.monitorInfo.rcMonitor;
                int offsetX = monitorRect.left;
                int offsetY = monitorRect.top;

                // Center window on primary monitor
                int windowWidth = rect.right - rect.left;
                int windowHeight = rect.bottom - rect.top;
                int newLeft = offsetX + (monitorRect.Width - windowWidth) / 2;
                int newTop = offsetY + (monitorRect.Height - windowHeight) / 2;

                rect.left = Math.Max(newLeft, offsetX);
                rect.top = Math.Max(newTop, offsetY);
                rect.right = rect.left + windowWidth;
                rect.bottom = rect.top + windowHeight;
            }
        }

        return rect;
    }

    private static bool IsValidPlacement(WINDOWPLACEMENT placement, RECT rect)
    {
        const int minDimension = 50;
        const int minimizedSentinel = -32000; // Some window states store -32000 coordinates when minimized/hidden.

        RECT rcNormal = placement.rcNormalPosition;

        if (rect.Width <= minDimension || rect.Height <= minDimension)
        {
            return false;
        }

        if (rect.left <= minimizedSentinel && rect.top <= minimizedSentinel)
        {
            return false;
        }

        if (rcNormal.Width <= minDimension || rcNormal.Height <= minDimension)
        {
            return false;
        }

        if (rcNormal.left <= minimizedSentinel && rcNormal.top <= minimizedSentinel)
        {
            return false;
        }

        if (placement.showCmd == SHOW_WINDOW_CMD.SW_HIDE)
        {
            return false;
        }

        HMONITOR hMonitor = PInvoke.MonitorFromRect(in rect, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONULL);
        if (hMonitor == 0)
        {
            return false;
        }

        RECT rcMonitor;
        unsafe
        {
            var monitorInfo = new MONITORINFO
            {
                cbSize = (uint)sizeof(MONITORINFO),
            };

            if (!PInvoke.GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                return false;
            }

            rcMonitor = monitorInfo.rcMonitor;
        }

        if (rect.right <= rcMonitor.left ||
            rect.bottom <= rcMonitor.top ||
            rect.left >= rcMonitor.right ||
            rect.top >= rcMonitor.bottom)
        {
            return false;
        }

        if (rcNormal.right <= rcMonitor.left ||
            rcNormal.bottom <= rcMonitor.top ||
            rcNormal.left >= rcMonitor.right ||
            rcNormal.top >= rcMonitor.bottom)
        {
            return false;
        }

        return true;
    }

    private static unsafe bool TryGetNativeWindowPlacement(HWND hWnd, out WINDOWPLACEMENT placement)
    {
        placement = new()
        {
            length = (uint)sizeof(WINDOWPLACEMENT)
        };

        bool gotPlacement = PInvoke.GetWindowPlacement(hWnd, ref placement);
        if (!gotPlacement)
        {
            Logger.E(TAG, "Failed to get window placement");
            return false;
        }

        return true;
    }

    public class SavedWindowState
    {
        public const int CURRENT_VERSION = 2;

        [JsonPropertyName("Version")]
        public int Version { get; init; } = 0;

        [JsonPropertyName("Placement")]
        public WindowPlacementDto Placement { get; init; }

        [JsonPropertyName("CurrentRect")]
        public RectDto CurrentRect { get; init; }

        [JsonPropertyName("MonitorName")]
        public string MonitorName { get; init; } = string.Empty;
    }

    public readonly struct WindowPlacementDto
    {
        [JsonPropertyName("flags")]
        public uint Flags { get; init; }

        [JsonPropertyName("showCmd")]
        public int ShowCmd { get; init; }

        [JsonPropertyName("MinPosition")]
        public PointDto MinPosition { get; init; }

        [JsonPropertyName("MaxPosition")]
        public PointDto MaxPosition { get; init; }

        [JsonPropertyName("NormalPosition")]
        public RectDto NormalPosition { get; init; }

        public static WindowPlacementDto FromNative(WINDOWPLACEMENT native) => new()
        {
            Flags = (uint)native.flags,
            ShowCmd = (int)native.showCmd,
            MinPosition = PointDto.FromNative(native.ptMinPosition),
            MaxPosition = PointDto.FromNative(native.ptMaxPosition),
            NormalPosition = RectDto.FromNative(native.rcNormalPosition)
        };

        public WINDOWPLACEMENT ToNative()
        {
            WINDOWPLACEMENT native = default;
            native.flags = (WINDOWPLACEMENT_FLAGS)Flags;
            native.showCmd = (SHOW_WINDOW_CMD)ShowCmd;
            native.ptMinPosition = MinPosition.ToNative();
            native.ptMaxPosition = MaxPosition.ToNative();
            native.rcNormalPosition = NormalPosition.ToNative();
            return native;
        }
    }

    public readonly struct PointDto
    {
        [JsonPropertyName("X")]
        public int X { get; init; }

        [JsonPropertyName("Y")]
        public int Y { get; init; }

        public static PointDto FromNative(Point point) => new()
        {
            X = point.X,
            Y = point.Y
        };

        public Point ToNative()
        {
            Point point = default;
            point.X = X;
            point.Y = Y;
            return point;
        }
    }

    public readonly struct RectDto
    {
        [JsonPropertyName("Left")]
        public int Left { get; init; }

        [JsonPropertyName("Top")]
        public int Top { get; init; }

        [JsonPropertyName("Right")]
        public int Right { get; init; }

        [JsonPropertyName("Bottom")]
        public int Bottom { get; init; }

        public static RectDto FromNative(RECT rect) => new()
        {
            Left = rect.left,
            Top = rect.top,
            Right = rect.right,
            Bottom = rect.bottom
        };

        public RECT ToNative()
        {
            RECT rect = default;
            rect.left = Left;
            rect.top = Top;
            rect.right = Right;
            rect.bottom = Bottom;
            return rect;
        }
    }
}
