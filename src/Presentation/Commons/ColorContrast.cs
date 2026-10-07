namespace Rok.Commons;

/// <summary>
/// WCAG 2.x contrast helpers (relative luminance and contrast ratio).
/// </summary>
public static class ColorContrast
{
    /// <summary>
    /// Returns the WCAG relative luminance of a color (0 = black, 1 = white). Alpha is ignored.
    /// </summary>
    public static double RelativeLuminance(Windows.UI.Color color) =>
        (0.2126 * Linearize(color.R)) + (0.7152 * Linearize(color.G)) + (0.0722 * Linearize(color.B));

    /// <summary>
    /// Returns the WCAG contrast ratio between two colors (from 1 to 21). The order of the arguments does not matter.
    /// </summary>
    public static double ContrastRatio(Windows.UI.Color first, Windows.UI.Color second)
    {
        double a = RelativeLuminance(first);
        double b = RelativeLuminance(second);

        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    /// <summary>
    /// Returns an opaque version of <paramref name="background"/>, darkened (hue preserved) just enough
    /// to reach <paramref name="minRatio"/> against <paramref name="foreground"/>. A background that already
    /// complies is returned unchanged (alpha forced to 255). When even black cannot reach the ratio, black is returned.
    /// </summary>
    public static Windows.UI.Color EnsureContrast(Windows.UI.Color background, Windows.UI.Color foreground, double minRatio)
    {
        Windows.UI.Color opaque = Windows.UI.Color.FromArgb(255, background.R, background.G, background.B);

        if (ContrastRatio(opaque, foreground) >= minRatio)
            return opaque;

        for (int step = 1; step <= 255; step++)
        {
            double factor = (255 - step) / 255.0;
            Windows.UI.Color candidate = Windows.UI.Color.FromArgb(
                255,
                (byte)Math.Round(opaque.R * factor),
                (byte)Math.Round(opaque.G * factor),
                (byte)Math.Round(opaque.B * factor));

            if (ContrastRatio(candidate, foreground) >= minRatio)
                return candidate;
        }

        return Windows.UI.Color.FromArgb(255, 0, 0, 0);
    }

    private static double Linearize(byte channel)
    {
        double value = channel / 255.0;

        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}