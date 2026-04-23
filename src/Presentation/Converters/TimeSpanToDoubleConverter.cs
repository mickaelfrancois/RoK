namespace Rok.Converters;

public partial class TimeSpanToDoubleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        TimeSpan timeSpan = (TimeSpan)value;
        return timeSpan.TotalSeconds;
    }


    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value != null)
            return TimeSpan.FromSeconds((double)value);
        else
            return TimeSpan.Zero;
    }
}
