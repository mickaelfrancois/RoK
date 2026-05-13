using Rok.Shared;

namespace Rok.ApplicationTests.Shared;

public class LanguageHelpersTests
{
    [Theory(DisplayName = "NormalizeLanguageCode should return two-letter ISO code for valid tags")]
    [InlineData("fr-FR", "fr")]
    [InlineData("en-US", "en")]
    [InlineData("de", "de")]
    [InlineData("EN", "en")]
    public void NormalizeLanguageCode_ValidTag_ShouldReturnTwoLetterCode(string tag, string expected)
    {
        // Act
        string result = LanguageHelpers.NormalizeLanguageCode(tag);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "NormalizeLanguageCode should return the default language for null or whitespace input")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeLanguageCode_NullOrWhitespace_ShouldReturnDefault(string? tag)
    {
        // Act
        string result = LanguageHelpers.NormalizeLanguageCode(tag, "fr");

        // Assert
        Assert.Equal("fr", result);
    }

    [Fact(DisplayName = "NormalizeLanguageCode should extract first two characters from unknown culture tags")]
    public void NormalizeLanguageCode_UnknownTag_ShouldExtractFirstTwoChars()
    {
        // Act
        string result = LanguageHelpers.NormalizeLanguageCode("xyz-INVALID-TAG", "en");

        // Assert
        Assert.Equal("xy", result);
    }

    [Fact(DisplayName = "NormalizeLanguageCode should return default when tag is too short to extract code")]
    public void NormalizeLanguageCode_TooShortUnknownTag_ShouldReturnDefault()
    {
        // Act
        string result = LanguageHelpers.NormalizeLanguageCode("x", "fr");

        // Assert
        Assert.Equal("fr", result);
    }
}
