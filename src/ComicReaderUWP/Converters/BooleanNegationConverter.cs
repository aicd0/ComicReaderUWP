// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Microsoft.UI.Xaml.Data;

namespace ComicReaderUWP.Converters;

/// <summary>
/// Value converter that translates true to false and vice versa.
/// </summary>
public class BooleanNegationConverter : IValueConverter
{
    public object Convert(object value, Type target_type, object parameter, string language)
    {
        bool? boxed_bool = value as bool?;
        bool bool_value = (boxed_bool != null && boxed_bool.Value);
        return !bool_value;
    }

    public object ConvertBack(object value, Type target_type, object parameter, string language)
    {
        bool? boxed_bool = (value as bool?);
        bool bool_value = (boxed_bool != null && boxed_bool.Value);
        return !bool_value;
    }
}