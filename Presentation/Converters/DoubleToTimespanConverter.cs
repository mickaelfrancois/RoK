using System.Globalization;

namespace Rok.Converters;

public partial class DoubleToTimespanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        TimeSpan ts = TimeSpan.FromSeconds((double)value);
        return $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is string stringData && (TimeSpan.TryParseExact(stringData, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out TimeSpan ts) ||
                TimeSpan.TryParse(stringData, CultureInfo.InvariantCulture, out ts)))
        {
            return ts.TotalSeconds;
        }

        return 0d;
    }
}
