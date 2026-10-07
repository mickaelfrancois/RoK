using Rok.Application.Player;
using Rok.Commons;

namespace Rok.ViewModels.Player;

/// <summary>
/// Resolves the background tint of the player bar: the brand blue, lightly blended with the dominant color of the current album.
/// </summary>
public static class PlayerBarTint
{
    /// <summary>
    /// Share of the album color in the blend; the brand blue keeps the rest so the bar stays recognizable.
    /// </summary>
    public const double AlbumColorWeight = 0.2;

    private const double MinContrastWithText = 4.5;

    private static readonly Windows.UI.Color _text = Windows.UI.Color.FromArgb(255, 255, 255, 255);

    /// <summary>
    /// Matches <c>Blue700</c>, the color behind <c>BrandPrimaryBrush</c>.
    /// </summary>
    private static readonly Windows.UI.Color _brand = Windows.UI.Color.FromArgb(255, 0x15, 0x45, 0x87);

    /// <summary>
    /// Returns an opaque tint readable under white text, or <c>default</c> (alpha 0) when the bar must keep its default brand color:
    /// not playing music, or no dominant color available.
    /// </summary>
    public static Windows.UI.Color Resolve(EPlaybackMode mode, Windows.UI.Color dominant)
    {
        if (mode != EPlaybackMode.Music || dominant.A == 0)
            return default;

        Windows.UI.Color blended = Blend(_brand, dominant, AlbumColorWeight);

        return ColorContrast.EnsureContrast(blended, _text, MinContrastWithText);
    }

    private static Windows.UI.Color Blend(Windows.UI.Color baseColor, Windows.UI.Color overlay, double weight)
    {
        return Windows.UI.Color.FromArgb(
            255,
            Mix(baseColor.R, overlay.R, weight),
            Mix(baseColor.G, overlay.G, weight),
            Mix(baseColor.B, overlay.B, weight));
    }

    private static byte Mix(byte from, byte to, double weight)
    {
        return (byte)Math.Round(from + ((to - from) * weight));
    }
}