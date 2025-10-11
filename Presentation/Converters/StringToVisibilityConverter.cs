namespace Rok.Converters;

public partial class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string && !string.IsNullOrEmpty((string)value))
            return Visibility.Visible;

        return Visibility.Collapsed;
    }


    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return "";
    }


    public bool InvertVisibility { get; set; }
}
