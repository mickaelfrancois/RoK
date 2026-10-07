namespace Rok.Converters;

/// <summary>
/// Turns the player bar tint color into a brush; a transparent color (alpha 0) yields <see cref="FallbackBrush"/>.
/// </summary>
public partial class PlayerBarTintBrushConverter : IValueConverter
{
    private static readonly Dictionary<uint, SolidColorBrush> _cache = new();

    /// <summary>
    /// Brush used when no tint applies (radio, no album, no dominant color).
    /// </summary>
    public Brush? FallbackBrush { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        Windows.UI.Color color = value is Windows.UI.Color c ? c : default;

        if (color.A == 0)
            return FallbackBrush ?? new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        uint key = ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
        if (_cache.TryGetValue(key, out SolidColorBrush? cached))
            return cached;

        SolidColorBrush brush = new(color);
        _cache[key] = brush;

        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        DependencyProperty.UnsetValue;
}