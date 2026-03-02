using Windows.Foundation;

namespace Rok.Converters;

public partial class ColorToGradientBrushConverter : IValueConverter
{
    private static readonly Dictionary<uint, LinearGradientBrush> _cache = new();

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        Windows.UI.Color color = value is Windows.UI.Color c ? c : default;

        if (color.A == 0)
            return new SolidColorBrush(Windows.UI.Color.FromArgb(0xAA, 0, 0, 0));

        uint key = ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
        if (_cache.TryGetValue(key, out LinearGradientBrush? cached))
            return cached;

        Windows.UI.Color topColor = Windows.UI.Color.FromArgb(178, color.R, color.G, color.B);
        Windows.UI.Color bottomColor = Windows.UI.Color.FromArgb(242, color.R, color.G, color.B);

        LinearGradientBrush brush = new()
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1)
        };

        brush.GradientStops.Add(new GradientStop { Color = topColor, Offset = 0.0 });
        brush.GradientStops.Add(new GradientStop { Color = bottomColor, Offset = 1.0 });

        _cache[key] = brush;
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        DependencyProperty.UnsetValue;
}