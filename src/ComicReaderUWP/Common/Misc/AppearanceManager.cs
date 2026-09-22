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

        backgroundBrush = AppSettingsModel.AppBackground switch
        {
            AppSettingsModel.AppBackgroundEnum.None => (Brush)Application.Current.Resources["AppBackgroundNone"],
            _ => new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
        };

        _themeBackgroundCache = backgroundBrush;
        return backgroundBrush;
    }
}
