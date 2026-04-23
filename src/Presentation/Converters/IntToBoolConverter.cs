namespace Rok.Converters;

public partial class IntToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int && (int)value > 0)
            return true;

        return false;
    }


    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue && boolValue)
            return true;

        return false;
    }
}
