// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Data.Models;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace ComicReader.Common.Misc;

internal class AppearanceManager
{
    public static AppearanceManager Instance { get; } = new();

    private Brush? _themeBackgroundCache = null;

    private AppearanceManager() { }

    public Brush GetThemeBackground()
    {
        Brush? backgroundBrush = _themeBackgroundCache;
        if (backgroundBrush is not null)
        {
            return backgroundBrush;
        }

        AppSettingsModel.AppBackgroundEnum backgroundEnum = AppSettingsModel.Instance.GetModel().Background;
        backgroundBrush = backgroundEnum switch
        {
            AppSettingsModel.AppBackgroundEnum.Acrylic => new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            _ => (Brush)Application.Current.Resources["AppBackgroundNone"],
        };

        _themeBackgroundCache = backgroundBrush;
        return backgroundBrush;
    }
}
