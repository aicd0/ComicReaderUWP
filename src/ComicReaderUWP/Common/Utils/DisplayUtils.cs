// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Views.AppWindows.Main;

using Microsoft.UI.Xaml;

namespace ComicReaderUWP.Common.Utils;

internal static class DisplayUtils
{
    public static double GetRasterizationScale(UIElement? element, double fallbackValue = 1.0)
    {
        if (element is null)
        {
            return fallbackValue;
        }

        MainWindow? window = App.Instance.WindowManager.GetWindowByXamlRoot(element.XamlRoot);
        if (window is null)
        {
            return fallbackValue;
        }

        return window.GetRasterizationScale(fallbackValue: fallbackValue);
    }
}
