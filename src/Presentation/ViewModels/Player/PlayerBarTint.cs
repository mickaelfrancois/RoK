using Rok.Application.Player;
using Rok.Commons;

namespace Rok.ViewModels.Player;

/// <summary>
/// Resolves the background tint of the player bar from the dominant color of the current album.
/// </summary>
public static class PlayerBarTint
{
    private const double MinContrastWithText = 4.5;

    private static readonly Windows.UI.Color _text = Windows.UI.Color.FromArgb(255, 255, 255, 255);

    /// <summary>
    /// Returns an opaque tint readable under white text, or <c>default</c> (alpha 0) when the bar must keep its default brand color:
    /// not playing music, or no dominant color available.
    /// </summary>
    public static Windows.UI.Color Resolve(EPlaybackMode mode, Windows.UI.Color dominant)
    {
        if (mode != EPlaybackMode.Music || dominant.A == 0)
            return default;

        return ColorContrast.EnsureContrast(dominant, _text, MinContrastWithText);
    }
}