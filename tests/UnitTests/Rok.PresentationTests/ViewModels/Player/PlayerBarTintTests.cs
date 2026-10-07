using Rok.Application.Player;
using Rok.Commons;
using Rok.ViewModels.Player;
using Windows.UI;

namespace Rok.PresentationTests.ViewModels.Player;

public class PlayerBarTintTests
{
    private static readonly Color White = Color.FromArgb(255, 255, 255, 255);

    [Fact(DisplayName = "resolve_returns_an_opaque_readable_tint_for_a_stored_dark_color_in_music_mode")]
    public void Resolve_ReturnsOpaqueReadableTint_ForStoredDarkColorInMusicMode()
    {
        // Arrange
        Color stored = Color.FromArgb(255, 0x2B, 0x17, 0x19);

        // Act
        Color tint = PlayerBarTint.Resolve(EPlaybackMode.Music, stored);

        // Assert
        Assert.Equal(255, tint.A);
        Assert.True(ColorContrast.ContrastRatio(tint, White) >= 4.5);
    }

    [Fact(DisplayName = "resolve_signals_fallback_when_there_is_no_dominant_color")]
    public void Resolve_SignalsFallback_WhenNoDominantColor()
    {
        // Act
        Color tint = PlayerBarTint.Resolve(EPlaybackMode.Music, default);

        // Assert
        Assert.Equal(0, tint.A);
    }

    [Theory(DisplayName = "resolve_signals_fallback_outside_music_mode")]
    [InlineData(EPlaybackMode.Radio)]
    [InlineData(EPlaybackMode.None)]
    public void Resolve_SignalsFallback_OutsideMusicMode(EPlaybackMode mode)
    {
        // Arrange
        Color stored = Color.FromArgb(255, 0x2B, 0x17, 0x19);

        // Act
        Color tint = PlayerBarTint.Resolve(mode, stored);

        // Assert
        Assert.Equal(0, tint.A);
    }

    [Fact(DisplayName = "resolve_forces_opaque_alpha_and_contrast_for_a_translucent_light_color")]
    public void Resolve_ForcesOpaqueAlphaAndContrast_ForTranslucentLightColor()
    {
        // Arrange
        Color translucent = Color.FromArgb(128, 255, 255, 0);

        // Act
        Color tint = PlayerBarTint.Resolve(EPlaybackMode.Music, translucent);

        // Assert
        Assert.Equal(255, tint.A);
        Assert.True(ColorContrast.ContrastRatio(tint, White) >= 4.5);
    }
}