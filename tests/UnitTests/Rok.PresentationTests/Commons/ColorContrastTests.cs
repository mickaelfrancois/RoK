using Rok.Commons;
using Windows.UI;

namespace Rok.PresentationTests.Commons;

public class ColorContrastTests
{
    private static readonly Color White = Color.FromArgb(255, 255, 255, 255);
    private static readonly Color Black = Color.FromArgb(255, 0, 0, 0);

    [Fact(DisplayName = "relative_luminance_is_one_for_white_and_zero_for_black")]
    public void RelativeLuminance_IsOneForWhiteAndZeroForBlack()
    {
        // Act & Assert
        Assert.Equal(1.0, ColorContrast.RelativeLuminance(White), 6);
        Assert.Equal(0.0, ColorContrast.RelativeLuminance(Black), 6);
    }

    [Fact(DisplayName = "contrast_ratio_is_21_between_white_and_black_in_any_order")]
    public void ContrastRatio_Is21BetweenWhiteAndBlack_InAnyOrder()
    {
        // Act & Assert
        Assert.Equal(21.0, ColorContrast.ContrastRatio(White, Black), 2);
        Assert.Equal(21.0, ColorContrast.ContrastRatio(Black, White), 2);
    }

    [Fact(DisplayName = "contrast_ratio_matches_the_aa_limit_around_767676")]
    public void ContrastRatio_MatchesAaLimit_Around767676()
    {
        // Act
        double passing = ColorContrast.ContrastRatio(White, Color.FromArgb(255, 0x76, 0x76, 0x76));
        double failing = ColorContrast.ContrastRatio(White, Color.FromArgb(255, 0x77, 0x77, 0x77));

        // Assert
        Assert.True(passing >= 4.5);
        Assert.InRange(passing, 4.5, 4.6);
        Assert.True(failing < 4.5);
        Assert.InRange(failing, 4.4, 4.5);
    }

    [Fact(DisplayName = "ensure_contrast_keeps_a_compliant_color_unchanged")]
    public void EnsureContrast_KeepsCompliantColorUnchanged()
    {
        // Arrange
        Color compliant = Color.FromArgb(255, 0x76, 0x76, 0x76);

        // Act
        Color result = ColorContrast.EnsureContrast(compliant, White, 4.5);

        // Assert
        Assert.Equal(compliant, result);
    }

    [Fact(DisplayName = "ensure_contrast_slightly_darkens_a_color_just_under_the_limit")]
    public void EnsureContrast_SlightlyDarkens_ColorJustUnderLimit()
    {
        // Arrange
        Color input = Color.FromArgb(255, 0x77, 0x77, 0x77);

        // Act
        Color result = ColorContrast.EnsureContrast(input, White, 4.5);

        // Assert
        Assert.True(ColorContrast.ContrastRatio(result, White) >= 4.5);
        Assert.True(result.R < input.R);
        Assert.True(input.R - result.R <= 4);
    }

    [Theory(DisplayName = "ensure_contrast_reaches_the_ratio_for_light_colors")]
    [InlineData(255, 255, 255)]
    [InlineData(255, 255, 0)]
    [InlineData(0, 255, 255)]
    public void EnsureContrast_ReachesRatio_ForLightColors(byte r, byte g, byte b)
    {
        // Act
        Color result = ColorContrast.EnsureContrast(Color.FromArgb(255, r, g, b), White, 4.5);

        // Assert
        Assert.True(ColorContrast.ContrastRatio(result, White) >= 4.5);
        Assert.Equal(255, result.A);
    }

    [Fact(DisplayName = "ensure_contrast_preserves_the_hue_of_a_vivid_red")]
    public void EnsureContrast_PreservesHue_OfVividRed()
    {
        // Act
        Color result = ColorContrast.EnsureContrast(Color.FromArgb(255, 0xE0, 0x20, 0x20), White, 4.5);

        // Assert
        Assert.True(ColorContrast.ContrastRatio(result, White) >= 4.5);
        Assert.True(result.R > result.G);
        Assert.True(result.R > result.B);
    }

    [Fact(DisplayName = "ensure_contrast_keeps_black_unchanged")]
    public void EnsureContrast_KeepsBlackUnchanged()
    {
        // Act
        Color result = ColorContrast.EnsureContrast(Black, White, 4.5);

        // Assert
        Assert.Equal(Black, result);
        Assert.Equal(21.0, ColorContrast.ContrastRatio(result, White), 2);
    }

    [Fact(DisplayName = "ensure_contrast_keeps_the_brand_primary_unchanged")]
    public void EnsureContrast_KeepsBrandPrimaryUnchanged()
    {
        // Arrange
        Color brandPrimary = Color.FromArgb(255, 0x15, 0x45, 0x87);

        // Act
        Color result = ColorContrast.EnsureContrast(brandPrimary, White, 4.5);

        // Assert
        Assert.Equal(brandPrimary, result);
        Assert.InRange(ColorContrast.ContrastRatio(result, White), 9.0, 10.0);
    }

    [Fact(DisplayName = "ensure_contrast_forces_the_result_to_be_opaque")]
    public void EnsureContrast_ForcesResultToBeOpaque()
    {
        // Act
        Color result = ColorContrast.EnsureContrast(Color.FromArgb(40, 0xFF, 0xFF, 0x00), White, 4.5);

        // Assert
        Assert.Equal(255, result.A);
        Assert.True(ColorContrast.ContrastRatio(result, White) >= 4.5);
    }
}