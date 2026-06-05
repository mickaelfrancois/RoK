namespace Rok.Converters;

/// <summary>
/// Derives a vivid accent brush from a dominant color (often very dark) by normalizing
/// its saturation and lightness while preserving the hue.
/// </summary>
public partial class ColorToAccentBrushConverter : IValueConverter
{
    private const double MinSaturation = 0.55;
    private const double GraySaturationThreshold = 0.05;
    private const double MinLightness = 0.45;
    private const double MaxLightness = 0.62;

    private static readonly Dictionary<uint, SolidColorBrush> _cache = new();

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        Windows.UI.Color color = value is Windows.UI.Color c ? c : default;

        if (color.A == 0)
            return new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

        uint key = ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
        if (_cache.TryGetValue(key, out SolidColorBrush? cached))
            return cached;

        SolidColorBrush brush = new(ToAccentColor(color));
        _cache[key] = brush;
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        DependencyProperty.UnsetValue;

    /// <summary>
    /// Normalizes a color into an accent: hue is preserved, saturation is boosted
    /// (unless the color is truly gray) and lightness is clamped into a visible range.
    /// </summary>
    public static Windows.UI.Color ToAccentColor(Windows.UI.Color color)
    {
        (double hue, double saturation, double lightness) = ToHsl(color);

        if (saturation >= GraySaturationThreshold)
            saturation = Math.Max(saturation, MinSaturation);

        lightness = Math.Clamp(lightness, MinLightness, MaxLightness);

        return FromHsl(hue, saturation, lightness);
    }

    private static (double Hue, double Saturation, double Lightness) ToHsl(Windows.UI.Color color)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double lightness = (max + min) / 2.0;
        double delta = max - min;

        if (delta == 0)
            return (0, 0, lightness);

        double saturation = delta / (1 - Math.Abs((2 * lightness) - 1));

        double hue;
        if (max == r)
            hue = 60 * (((g - b) / delta) % 6);
        else if (max == g)
            hue = 60 * (((b - r) / delta) + 2);
        else
            hue = 60 * (((r - g) / delta) + 4);

        if (hue < 0)
            hue += 360;

        return (hue, saturation, lightness);
    }

    private static Windows.UI.Color FromHsl(double hue, double saturation, double lightness)
    {
        double c = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        double x = c * (1 - Math.Abs((hue / 60 % 2) - 1));
        double m = lightness - (c / 2);

        (double r, double g, double b) = hue switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };

        return Windows.UI.Color.FromArgb(
            255,
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }
}
