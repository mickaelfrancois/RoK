using Microsoft.UI;

namespace Rok.Converters;

public partial class IntToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int intValue)
        {
            if (intValue > 0)
                return new SolidColorBrush(Colors.Green);
            else if (intValue < 0)
                return new SolidColorBrush(Colors.Red);
        }

        return new SolidColorBrush(Colors.Gray);
    }


    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is SolidColorBrush brush)
        {
            if (brush.Color == Colors.Green)
                return 1;
            else if (brush.Color == Colors.Red)
                return -1;
        }

        return 0;
    }
}