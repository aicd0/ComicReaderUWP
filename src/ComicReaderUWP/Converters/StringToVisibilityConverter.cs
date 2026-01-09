// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace ComicReaderUWP.Converters;

/// <summary>
/// Value converter that translates not empty string to <see cref="Visibility.Visible"/> and empty string
/// to <see cref="Visibility.Collapsed"/>.
/// </summary>
public partial class StringToVisibilityConverter : IValueConverter
{
    public static Visibility Convert(string? value)
    {
        return string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object Convert(object value, Type target_type, object parameter, string language)
    {
        string? stringValue = value as string;
        return Convert(stringValue);
    }

    public object ConvertBack(object value, Type target_type, object parameter, string language)
    {
        var visibility = (value as Visibility?);
        return (visibility != null && visibility.Value == Visibility.Visible) ? "?" : "";
    }
}
