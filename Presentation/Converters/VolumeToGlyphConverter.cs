namespace Rok.Converters;

public partial class VolumeToGlyphConverter : IValueConverter
{
    private const double VolumeEpsilon = 0.0001;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        double volume = (double)value;

        if (Math.Abs(volume) < VolumeEpsilon)
            return "\xE992";
        else if (volume <= 33)
            return "\xE993";
        else if (volume <= 55)
            return "\xE994";
        else
            return "\xE995";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
