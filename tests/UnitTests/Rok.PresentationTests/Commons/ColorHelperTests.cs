using Rok.Commons;
using Windows.UI;

namespace Rok.PresentationTests.Commons;

public class ColorHelperTests
{
    [Theory(DisplayName = "FromArgb should decompose a 32-bit ARGB value into ARGB components")]
    [InlineData(0x00000000L, 0, 0, 0, 0)]
    [InlineData(0xFFFFFFFFL, 255, 255, 255, 255)]
    [InlineData(0xFF112233L, 255, 0x11, 0x22, 0x33)]
    [InlineData(0x80FF0000L, 0x80, 0xFF, 0, 0)]
    public void FromArgb_ShouldDecomposeArgbValue(long argb, byte expectedAlpha, byte expectedRed, byte expectedGreen, byte expectedBlue)
    {
        // Act
        Color color = ColorHelper.FromArgb(argb);

        // Assert
        Assert.Equal(expectedAlpha, color.A);
        Assert.Equal(expectedRed, color.R);
        Assert.Equal(expectedGreen, color.G);
        Assert.Equal(expectedBlue, color.B);
    }

    [Theory(DisplayName = "ToArgb should encode a Color into a 32-bit ARGB value")]
    [InlineData(0, 0, 0, 0, 0L)]
    [InlineData(255, 255, 255, 255, 0xFFFFFFFFL)]
    [InlineData(255, 0x11, 0x22, 0x33, 0xFF112233L)]
    [InlineData(0x80, 0xFF, 0, 0, 0x80FF0000L)]
    public void ToArgb_ShouldEncodeColorIntoArgb(byte alpha, byte red, byte green, byte blue, long expected)
    {
        // Arrange
        Color color = Color.FromArgb(alpha, red, green, blue);

        // Act
        long argb = ColorHelper.ToArgb(color);

        // Assert
        Assert.Equal(expected, argb);
    }

    [Theory(DisplayName = "ToArgb and FromArgb should round-trip without loss")]
    [InlineData(0xFF112233L)]
    [InlineData(0x12345678L)]
    [InlineData(0x00FFFFFFL)]
    public void RoundTrip_ShouldPreserveColor(long argb)
    {
        // Act
        Color color = ColorHelper.FromArgb(argb);
        long back = ColorHelper.ToArgb(color);

        // Assert
        Assert.Equal(argb, back);
    }
}