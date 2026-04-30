using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Rok.Converters;

public partial class LyricColorConverter : IValueConverter
{
    private static readonly SolidColorBrush[] Palette = new[]
    {
        new SolidColorBrush(Color.FromArgb(255, 255, 190, 11)),
        new SolidColorBrush(Color.FromArgb(255, 255, 0, 110)),
        new SolidColorBrush(Color.FromArgb(255, 58, 134, 255)),
        new SolidColorBrush(Color.FromArgb(255, 255, 122, 0)),
    };

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int index && index >= 0)
            return Palette[index % Palette.Length];

        return Palette[0];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
