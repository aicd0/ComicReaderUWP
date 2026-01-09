// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Microsoft.UI.Xaml.Data;

namespace ComicReaderUWP.Converters;

/// <summary>
/// Value converter that translates true to <see cref="\uE76C"/> and false
/// to <see cref="\uE70D"/>.
/// </summary>
public class BooleanToCollapseGlyphConverter : IValueConverter
{
    public static string Convert(bool value)
    {
        return value ? "\uE76C" : "\uE70D";
    }

    public object Convert(object value, Type target_type, object parameter, string language)
    {
        bool? boxed_bool = value as bool?;
        bool bool_value = boxed_bool != null && boxed_bool.Value;
        return Convert(bool_value);
    }

    public object ConvertBack(object value, Type target_type, object parameter, string language)
    {
        string? glyph = value as string;
        return glyph == "\uE76C";
    }
}
