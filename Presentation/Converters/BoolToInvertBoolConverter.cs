namespace Rok.Converters;

public partial class BoolToInvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool)
        {
            bool boolValue = (bool)value;
            return !boolValue;
        }
        else
        {
            return false;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException("ConvertBack() of BoolToInvertBoolConverter is not implemented");
    }
}
