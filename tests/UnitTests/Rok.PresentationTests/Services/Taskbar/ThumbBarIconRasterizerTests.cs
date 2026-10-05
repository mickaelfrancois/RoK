using Rok.Services.Taskbar;

namespace Rok.PresentationTests.Services.Taskbar;

public class ThumbBarIconRasterizerTests
{
    private const uint White = 0xFFFFFFFF;

    [Fact(DisplayName = "rasterized_icon_has_the_requested_size")]
    public void Render_ReturnsSizeSquaredPixels()
    {
        // Arrange
        const int size = 20;

        // Act
        var pixels = ThumbBarIconRasterizer.Render(ThumbBarGlyph.Play, size, White);

        // Assert
        Assert.Equal(size * size, pixels.Length);
    }

    [Theory(DisplayName = "rasterized_icon_keeps_transparent_corners_and_an_opaque_center")]
    [InlineData(ThumbBarGlyph.Play)]
    [InlineData(ThumbBarGlyph.Pause)]
    [InlineData(ThumbBarGlyph.Previous)]
    [InlineData(ThumbBarGlyph.Next)]
    public void Render_KeepsCornersTransparentAndHasOpaquePixel(ThumbBarGlyph glyph)
    {
        // Arrange
        const int size = 16;

        // Act
        var pixels = ThumbBarIconRasterizer.Render(glyph, size, White);

        // Assert
        Assert.Equal(0u, pixels[0] >> 24);
        Assert.Equal(0u, pixels[size - 1] >> 24);
        Assert.Equal(0u, pixels[(size - 1) * size] >> 24);
        Assert.Equal(0u, pixels[(size * size) - 1] >> 24);
        Assert.Contains(pixels, pixel => pixel >> 24 == 255);
    }

    [Fact(DisplayName = "play_and_pause_glyphs_differ")]
    public void Render_PlayAndPauseDiffer()
    {
        // Arrange
        const int size = 16;

        // Act
        var play = ThumbBarIconRasterizer.Render(ThumbBarGlyph.Play, size, White);
        var pause = ThumbBarIconRasterizer.Render(ThumbBarGlyph.Pause, size, White);

        // Assert
        Assert.NotEqual(play, pause);
    }

    [Fact(DisplayName = "previous_and_next_glyphs_differ")]
    public void Render_PreviousAndNextDiffer()
    {
        // Arrange
        const int size = 16;

        // Act
        var previous = ThumbBarIconRasterizer.Render(ThumbBarGlyph.Previous, size, White);
        var next = ThumbBarIconRasterizer.Render(ThumbBarGlyph.Next, size, White);

        // Assert
        Assert.NotEqual(previous, next);
    }

    [Fact(DisplayName = "rasterized_pixels_are_premultiplied")]
    public void Render_PixelsArePremultiplied()
    {
        // Arrange
        const int size = 16;

        // Act
        var pixels = ThumbBarIconRasterizer.Render(ThumbBarGlyph.Play, size, White);

        // Assert
        Assert.All(pixels, pixel =>
        {
            var alpha = pixel >> 24;

            Assert.True(((pixel >> 16) & 0xFF) <= alpha);
            Assert.True(((pixel >> 8) & 0xFF) <= alpha);
            Assert.True((pixel & 0xFF) <= alpha);
        });
    }
}