// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Drawing;
using System.Text.Json.Serialization;

using ComicReaderUWP.Common.Utils;
using ComicReaderUWP.Core.Common.DebugTools;

using Windows.Win32;

namespace ComicReaderUWP.Views.AppWindows.Main;

internal class WindowPlacementManager(MainWindow window)
{
    private const string TAG = nameof(WindowPlacementManager);

    private readonly MainWindow _window = window;

    public SavedWindowState? GetWindowPlacement()
    {
        var hWnd = new Windows.Win32.Foundation.HWND(_window.WindowHandle);

        Windows.Win32.UI.WindowsAndMessaging.WINDOWPLACEMENT placement;
        unsafe
        {
            placement = new()
            {
                length = (uint)sizeof(Windows.Win32.UI.WindowsAndMessaging.WINDOWPLACEMENT)
            };
        }
        bool gotPlacement = PInvoke.GetWindowPlacement(hWnd, ref placement);

        if (!gotPlacement)
        {
            Logger.E(TAG, "Failed to get window placement.");
            return null;
        }

        bool gotFrameRect;
        Windows.Win32.Foundation.RECT currentRect = default;
        unsafe
        {
            int hr = PInvoke.DwmGetWindowAttribute(
            hWnd,
            Windows.Win32.Graphics.Dwm.DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS,
            &currentRect,
            (uint)sizeof(Windows.Win32.Foundation.RECT));
            gotFrameRect = hr >= 0;
        }

        if (!gotFrameRect)
        {
            PInvoke.GetWindowRect(hWnd, out currentRect);
        }

        if (!IsValidPlacement(placement, currentRect))
        {
            Logger.I(TAG, "Invalid window placement, not saving.");
            return null;
        }

        var state = new SavedWindowState
        {
            Version = SavedWindowState.CURRENT_VERSION,
            Placement = WindowPlacementDto.FromNative(placement),
            CurrentRect = RectDto.FromNative(currentRect)
        };
        return state;
    }

    public void RestoreWindowPlacement(SavedWindowState state)
    {
        if (state.Version < SavedWindowState.CURRENT_VERSION)
        {
            return;
        }

        var hWnd = new Windows.Win32.Foundation.HWND(_window.WindowHandle);
        Windows.Win32.UI.WindowsAndMessaging.WINDOWPLACEMENT windowPlacement = state.Placement.ToNative();
        unsafe
        {
            windowPlacement.length = (uint)sizeof(Windows.Win32.UI.WindowsAndMessaging.WINDOWPLACEMENT);
        }

        Windows.Win32.Foundation.RECT currentRect = state.CurrentRect.ToNative();
        if (!IsValidPlacement(windowPlacement, currentRect))
        {
            Logger.E(TAG, "Invalid window placement, not restoring.");
            return;
        }

        PInvoke.SetWindowPlacement(hWnd, in windowPlacement);
        var hWndTop = new Windows.Win32.Foundation.HWND(0);
        PInvoke.SetWindowPos(hWnd, hWndTop, currentRect.left, currentRect.top, currentRect.Width, currentRect.Height,
            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOZORDER |
            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
    }

    private bool IsValidPlacement(Windows.Win32.UI.WindowsAndMessaging.WINDOWPLACEMENT placement, Windows.Win32.Foundation.RECT rect)
    {
        DisplayUtils.GetScreenSize(out int screenWidth, out int screenHeight);

        const int minDimension = 50;
        if (rect.Width <= minDimension || rect.Height <= minDimension)
        {
            return false;
        }

        if (rect.right <= 0 || rect.bottom <= 0 || rect.left >= screenWidth || rect.top >= screenHeight)
        {
            return false;
        }

        // Some window states store -32000 coordinates when minimized/hidden.
        const int minimizedSentinel = -32000;
        if (rect.left <= minimizedSentinel && rect.top <= minimizedSentinel)
        {
            return false;
        }

        Windows.Win32.Foundation.RECT normal = placement.rcNormalPosition;
        if (normal.Width <= minDimension || normal.Height <= minDimension)
        {
            return false;
        }

        if (normal.right <= 0 || normal.bottom <= 0 || normal.left >= screenWidth || normal.top >= screenHeight)
        {
            return false;
        }

        if (normal.left <= minimizedSentinel && normal.top <= minimizedSentinel)
        {
            return false;
        }

        if (placement.showCmd == Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_HIDE)
        {
            return false;
        }

        return true;
    }

    public class SavedWindowState
    {
        public const int CURRENT_VERSION = 1;

        [JsonPropertyName("Version")]
        public int Version { get; init; } = 0;

        [JsonPropertyName("Placement")]
        public WindowPlacementDto Placement { get; init; }

        [JsonPropertyName("CurrentRect")]
        public RectDto CurrentRect { get; init; }
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

        public static WindowPlacementDto FromNative(Windows.Win32.UI.WindowsAndMessaging.WINDOWPLACEMENT native)
            => new()
            {
                Flags = (uint)native.flags,
                ShowCmd = (int)native.showCmd,
                MinPosition = PointDto.FromNative(native.ptMinPosition),
                MaxPosition = PointDto.FromNative(native.ptMaxPosition),
                NormalPosition = RectDto.FromNative(native.rcNormalPosition)
            };

        public Windows.Win32.UI.WindowsAndMessaging.WINDOWPLACEMENT ToNative()
        {
            Windows.Win32.UI.WindowsAndMessaging.WINDOWPLACEMENT native = default;
            native.flags = (Windows.Win32.UI.WindowsAndMessaging.WINDOWPLACEMENT_FLAGS)Flags;
            native.showCmd = (Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD)ShowCmd;
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

        public static PointDto FromNative(System.Drawing.Point point)
            => new()
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

        public static RectDto FromNative(Windows.Win32.Foundation.RECT rect)
            => new()
            {
                Left = rect.left,
                Top = rect.top,
                Right = rect.right,
                Bottom = rect.bottom
            };

        public Windows.Win32.Foundation.RECT ToNative()
        {
            Windows.Win32.Foundation.RECT rect = default;
            rect.left = Left;
            rect.top = Top;
            rect.right = Right;
            rect.bottom = Bottom;
            return rect;
        }
    }
}
