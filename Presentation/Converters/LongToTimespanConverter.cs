namespace Rok.Converters;

public partial class LongToTimespanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        long longValue = (long)value;
        TimeSpan ts = TimeSpan.FromSeconds(longValue);
        return $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return 0L;
    }
}
