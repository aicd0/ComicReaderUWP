// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReaderUWP.Data.Models.Misc;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace ComicReaderUWP.Common.Misc;

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

        AppSettingsModel.AppBackgroundEnum backgroundEnum = AppSettingsModel.GetModel().Background;
        backgroundBrush = backgroundEnum switch
        {
            AppSettingsModel.AppBackgroundEnum.Acrylic => new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            _ => (Brush)Application.Current.Resources["AppBackgroundNone"],
        };

        _themeBackgroundCache = backgroundBrush;
        return backgroundBrush;
    }
}
