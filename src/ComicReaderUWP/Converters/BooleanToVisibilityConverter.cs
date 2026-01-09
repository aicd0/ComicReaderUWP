// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace ComicReaderUWP.Converters;

/// <summary>
/// Value converter that translates true to <see cref="Visibility.Visible"/> and false
/// to <see cref="Visibility.Collapsed"/>.
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    public static Visibility Convert(bool visibility)
    {
        return visibility ? Visibility.Visible : Visibility.Collapsed;
    }

    public object Convert(object value, Type target_type, object parameter, string language)
    {
        bool? boxed_bool = (value as bool?);
        bool bool_value = (boxed_bool != null && boxed_bool.Value);
        return BooleanToVisibilityConverter.Convert(bool_value);
    }

    public object ConvertBack(object value, Type target_type, object parameter, string language)
    {
        var visibility = (value as Visibility?);
        return (visibility != null && visibility.Value == Visibility.Visible);
    }
}

/// <summary>
/// Value converter that translates false to <see cref="Visibility.Visible"/> and true
/// to <see cref="Visibility.Collapsed"/>.
/// </summary>
public class BooleanToVisibilityNegationConverter : IValueConverter
{
    public object Convert(object value, Type target_type, object parameter, string language)
    {
        bool? boxed_bool = (value as bool?);
        bool bool_value = (boxed_bool != null && boxed_bool.Value);
        return BooleanToVisibilityConverter.Convert(!bool_value);
    }

    public object ConvertBack(object value, Type target_type, object parameter, string language)
    {
        var visibility = (value as Visibility?);
        return (visibility != null && visibility.Value != Visibility.Visible);
    }
}