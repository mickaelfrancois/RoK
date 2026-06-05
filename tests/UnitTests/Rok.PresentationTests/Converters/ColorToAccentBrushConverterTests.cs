using Rok.Converters;
using Windows.UI;

namespace Rok.PresentationTests.Converters;

public class ColorToAccentBrushConverterTests
{
    [Fact(DisplayName = "ToAccentColor should brighten a near black dominant color while keeping its hue")]
    public void ToAccentColor_ShouldBrightenNearBlackColor_KeepingHue()
    {
        // Arrange: real stored dominant color FF2B1719 (dark brownish red)
        Color stored = Color.FromArgb(0xFF, 0x2B, 0x17, 0x19);

        // Act
        Color accent = ColorToAccentBrushConverter.ToAccentColor(stored);

        // Assert: clearly visible and still red-dominant
        Assert.Equal(0xFF, accent.A);
        Assert.True(accent.R > 120, $"expected a bright red component, got {accent.R}");
        Assert.True(accent.R > accent.G && accent.R > accent.B, "hue should stay red-dominant");
    }

    [Fact(DisplayName = "ToAccentColor should keep a gray color gray instead of inventing a hue")]
    public void ToAccentColor_ShouldKeepGrayColorGray()
    {
        // Arrange
        Color gray = Color.FromArgb(0xFF, 0x20, 0x20, 0x20);

        // Act
        Color accent = ColorToAccentBrushConverter.ToAccentColor(gray);

        // Assert: lifted to a visible lightness but still achromatic
        Assert.Equal(accent.R, accent.G);
        Assert.Equal(accent.G, accent.B);
        Assert.True(accent.R > 0x20, "gray should be lifted to a visible lightness");
    }

    [Fact(DisplayName = "ToAccentColor should tone down an overly bright color into the accent range")]
    public void ToAccentColor_ShouldToneDownOverlyBrightColor()
    {
        // Arrange
        Color nearWhite = Color.FromArgb(0xFF, 0xFA, 0xFA, 0xFA);

        // Act
        Color accent = ColorToAccentBrushConverter.ToAccentColor(nearWhite);

        // Assert: clamped below the max lightness (0.62 * 255 ≈ 158)
        Assert.True(accent.R < 0xC0, $"expected lightness clamped down, got {accent.R}");
    }

    [Fact(DisplayName = "ToAccentColor should preserve a vivid color hue and keep it vivid")]
    public void ToAccentColor_ShouldKeepVividColorVivid()
    {
        // Arrange
        Color vividBlue = Color.FromArgb(0xFF, 0x20, 0x60, 0xE0);

        // Act
        Color accent = ColorToAccentBrushConverter.ToAccentColor(vividBlue);

        // Assert
        Assert.True(accent.B > accent.R && accent.B > accent.G, "hue should stay blue-dominant");
        Assert.True(accent.B > 140, $"expected a vivid blue component, got {accent.B}");
    }
}
