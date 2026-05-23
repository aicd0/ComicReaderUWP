// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Drawing;

using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Views.AppWindows.Main;

using Microsoft.UI;
using Microsoft.UI.Windowing;

using Windows.Win32;

namespace ComicReaderUWP.Common.Utils;

internal static class DisplayUtils
{
    private const string TAG = nameof(DisplayUtils);

    private static double sRawPixelPerPixel = -1;

    public static void GetScreenSize(out int width, out int height)
    {
        using var graphics = Graphics.FromHwnd(nint.Zero);
        Windows.Win32.Graphics.Gdi.HDC hdc = new(graphics.GetHdc());
        width = PInvoke.GetDeviceCaps(hdc, Windows.Win32.Graphics.Gdi.GET_DEVICE_CAPS_INDEX.DESKTOPHORZRES);
        height = PInvoke.GetDeviceCaps(hdc, Windows.Win32.Graphics.Gdi.GET_DEVICE_CAPS_INDEX.DESKTOPVERTRES);
    }

    public static double GetRawPixelPerPixel()
    {
        if (sRawPixelPerPixel < 0)
        {
            try
            {
                sRawPixelPerPixel = GetScaleAdjustment();
            }
            catch (Exception ex)
            {
                Logger.F(TAG, "GetRawPixelPerPixel", ex);
                return 1.0;
            }
        }

        return sRawPixelPerPixel;
    }

    private static double GetScaleAdjustment()
    {
        MainWindow? window = App.Instance.WindowManager.GetAnyWindow();
        if (window is null)
        {
            Logger.AssertNotReachHere("A10F68C0A70A9EC2");
            return 1.0;
        }

        WindowId windowId = Win32Interop.GetWindowIdFromWindow(window.WindowHandle);
        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        Windows.Win32.Graphics.Gdi.HMONITOR hMonitor = new(Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId));
        int returnCode = PInvoke.GetDpiForMonitor(hMonitor, Windows.Win32.UI.HiDpi.MONITOR_DPI_TYPE.MDT_DEFAULT, out uint dpiX, out uint _);
        if (returnCode != 0)
        {
            throw new Exception("Unable get DPI for the current monitor");
        }

        uint scaleFactorPercent = (uint)(((long)dpiX * 100 + (96 >> 1)) / 96);
        return scaleFactorPercent / 100.0;
    }
}
