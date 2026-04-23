using System.Globalization;

namespace Rok.Converters;

public partial class NumberToFormattedStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int numberInt)
            return numberInt.ToString("N0", CultureInfo.CurrentCulture);

        if (value is double numberDouble)
            return numberDouble.ToString("N0", CultureInfo.CurrentCulture);

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException("ConvertBack is not supported.");
    }
}
