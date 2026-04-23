namespace Rok.Converters;

public sealed class SecondsToReadableConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not IConvertible convertible)
            return value;

        TimeSpan t = TimeSpan.FromSeconds(Math.Abs(System.Convert.ToDouble(convertible)));

        return $"{(int)t.TotalMinutes:D2}min.";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
