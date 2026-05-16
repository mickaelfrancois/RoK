using Microsoft.UI.Xaml.Data;

namespace Rok.Converters;

public class FloatToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is float floatValue)
        {
            return floatValue >= 0 ? $"+{floatValue:F1} dB" : $"{floatValue:F1} dB";
        }

        return "0.0 dB";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}