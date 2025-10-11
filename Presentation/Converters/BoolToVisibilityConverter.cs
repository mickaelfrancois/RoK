namespace Rok.Converters;

public partial class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool && (bool)value)
            return Visibility.Visible;

        return Visibility.Collapsed;
    }


    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility visibility && visibility == Visibility.Visible)
            return true;

        return false;
    }
}
