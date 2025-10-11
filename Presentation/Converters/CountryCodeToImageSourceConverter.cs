namespace Rok.Converters;

public partial class CountryCodeToImageSourceConverter : IValueConverter
{
    private const string FallbackCountryCode = "fr";

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        string? countryCode = (value as string)?.Trim();

        if (string.IsNullOrWhiteSpace(countryCode))
            countryCode = FallbackCountryCode;
        else
            countryCode = countryCode.ToLowerInvariant();

        try
        {
            return new BitmapImage(new Uri($"ms-appx:///Assets/Flags/{countryCode}.png"));
        }
        catch
        {
            return new BitmapImage(new Uri($"ms-appx:///Assets/Flags/{FallbackCountryCode}.png"));
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
