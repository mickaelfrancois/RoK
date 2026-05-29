using Rok.Application.Validation;

namespace Rok.Converters;

public partial class StringToImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        string? url = (value as string)?.Trim();

        if (!HttpUriValidation.IsAbsoluteHttpUri(url))
            return null;

        try
        {
            return new BitmapImage(new Uri(url!));
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
