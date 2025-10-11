namespace Rok.Converters;

public partial class StringToUriConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string strValue && !string.IsNullOrEmpty(strValue))
            return new UriBuilder(strValue).Uri;

        return new Uri("https://novamusic.fpc-france.com");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return "";
    }


    public bool InvertVisibility { get; set; }
}
