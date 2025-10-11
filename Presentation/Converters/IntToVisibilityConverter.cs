namespace Rok.Converters;

public partial class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int && (int)value > 0)
            return Visibility.Visible;

        return Visibility.Collapsed;
    }


    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility && (Visibility)value == Visibility.Visible)
            return 1;

        return 0;
    }


    public bool InvertVisibility { get; set; }


}
