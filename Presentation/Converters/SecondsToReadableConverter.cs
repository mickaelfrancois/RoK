namespace Rok.Converters;

public sealed class SecondsToReadableConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not IConvertible convertible)
            return value;

        TimeSpan t = TimeSpan.FromSeconds(Math.Abs(System.Convert.ToDouble(convertible)));
        return $"{(int)t.TotalDays:D2}:{t.Hours:D2}:{t.Minutes:D2}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
