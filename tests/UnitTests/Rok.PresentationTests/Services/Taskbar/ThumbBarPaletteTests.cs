using Rok.Services.Taskbar;

namespace Rok.PresentationTests.Services.Taskbar;

public class ThumbBarPaletteTests
{
    [Fact(DisplayName = "glyph_color_contrasts_with_the_taskbar_theme")]
    public void GlyphColor_ContrastsWithTheme()
    {
        // Arrange
        // Act
        var onLight = ThumbBarPalette.GlyphColor(true);
        var onDark = ThumbBarPalette.GlyphColor(false);

        // Assert
        Assert.Equal(0xFF1F1F1Fu, onLight);
        Assert.Equal(0xFFFFFFFFu, onDark);
    }
}