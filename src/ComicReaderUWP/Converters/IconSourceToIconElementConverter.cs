// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace ComicReaderUWP.Converters;

internal partial class IconSourceToIconElementConverter : IValueConverter
{
    public static IconElement? Convert(IconSource? value)
    {
        IconElement CreateFontIcon(FontIconSource s)
        {
            var icon = new FontIcon
            {
                Glyph = s.Glyph,
                FontFamily = s.FontFamily,
                FontStyle = s.FontStyle,
                FontSize = s.FontSize,
                FontWeight = s.FontWeight,
                IsTextScaleFactorEnabled = s.IsTextScaleFactorEnabled,
                MirroredWhenRightToLeft = s.MirroredWhenRightToLeft,
            };

            if (s.Foreground is not null)
            {
                icon.Foreground = s.Foreground;
            }

            return icon;
        }

        return value switch
        {
            SymbolIconSource s => new SymbolIcon
            {
                Symbol = s.Symbol,
            },
            BitmapIconSource s => new BitmapIcon
            {
                UriSource = s.UriSource,
                ShowAsMonochrome = s.ShowAsMonochrome,
            },
            FontIconSource s => CreateFontIcon(s),
            PathIconSource s => new PathIcon
            {
                Data = s.Data,
            },
            _ => null
        };
    }

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        var typedValue = value as IconSource;
        return Convert(typedValue);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException("IconConverter does not support ConvertBack.");
    }
}
