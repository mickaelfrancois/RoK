﻿namespace Rok.Converters;

public partial class BoolToInvertVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool && (bool)value)
            return Visibility.Collapsed;

        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility && (Visibility)value == Visibility.Visible)
            return false;

        return true;
    }
}
