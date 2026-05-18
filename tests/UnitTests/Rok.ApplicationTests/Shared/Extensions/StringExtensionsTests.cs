using Rok.Shared.Extensions;

namespace Rok.ApplicationTests.Shared.Extensions;

public class StringExtensionsTests
{
    [Theory(DisplayName = "ToFileName should strip the path-invalid characters slash question mark and colon")]
    [InlineData("a/b?c:d", "abcd")]
    [InlineData("clean", "clean")]
    public void ToFileName_ShouldStripInvalidCharacters(string input, string expected)
    {
        // Act
        string result = input.ToFileName();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "ToFileName should trim trailing dots")]
    public void ToFileName_ShouldTrimTrailingDots()
    {
        // Act
        string result = "song...".ToFileName();

        // Assert
        Assert.Equal("song", result);
    }

    [Theory(DisplayName = "Capitalize should uppercase the first letter and lowercase the rest")]
    [InlineData("HELLO", "Hello")]
    [InlineData("a", "a")]
    [InlineData("", "")]
    [InlineData("mIxED", "Mixed")]
    public void Capitalize_ShouldNormalizeFirstLetter(string input, string expected)
    {
        // Act
        string result = input.Capitalize();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "GetNameFirstLetter should return uppercase letter or hash placeholder")]
    [InlineData("apple", "A")]
    [InlineData("Étoile", "É")]
    [InlineData("", "#123")]
    [InlineData("123abc", "#123")]
    [InlineData("#hash", "#123")]
    public void GetNameFirstLetter_ShouldReturnExpectedBucket(string input, string expected)
    {
        // Act
        string result = StringExtensions.GetNameFirstLetter(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "IsDifferent should return true for differing strings ignoring case")]
    [InlineData("abc", "abc", false)]
    [InlineData("abc", "ABC", false)]
    [InlineData("abc", "abd", true)]
    public void IsDifferent_ShouldCompareCaseInsensitive(string a, string b, bool expected)
    {
        // Act
        bool result = a.IsDifferent(b);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "AreEquals should treat null as empty string")]
    [InlineData(null, null, true)]
    [InlineData("", null, true)]
    [InlineData(null, "", true)]
    [InlineData("a", "a", true)]
    [InlineData("a", "b", false)]
    public void AreEquals_ShouldTreatNullAsEmpty(string? a, string? b, bool expected)
    {
        // Act
        bool result = a.AreEquals(b);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "AreDifferent should be the inverse of AreEquals")]
    [InlineData(null, null, false)]
    [InlineData("a", null, true)]
    [InlineData("a", "a", false)]
    [InlineData("a", "b", true)]
    public void AreDifferent_ShouldBeInverseOfAreEquals(string? a, string? b, bool expected)
    {
        // Act
        bool result = a.AreDifferent(b);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "NormalizeIndexedName should fold unicode dash variants to ascii hyphen")]
    [InlineData("Pro-pain", "Pro-pain")]
    [InlineData("Pro‐pain", "Pro-pain")]
    [InlineData("Pro‑pain", "Pro-pain")]
    [InlineData("Pro‒pain", "Pro-pain")]
    [InlineData("Pro–pain", "Pro-pain")]
    [InlineData("Pro—pain", "Pro-pain")]
    [InlineData("Pro―pain", "Pro-pain")]
    [InlineData("Pro−pain", "Pro-pain")]
    [InlineData("Pro﹘pain", "Pro-pain")]
    [InlineData("Pro﹣pain", "Pro-pain")]
    [InlineData("Pro－pain", "Pro-pain")]
    public void NormalizeIndexedName_ShouldFoldUnicodeDashes_ToAsciiHyphen(string input, string expected)
    {
        // Act
        string result = input.NormalizeIndexedName();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "NormalizeIndexedName should fold non-breaking and zero-width spaces to a regular space")]
    [InlineData("Daft Punk", "Daft Punk")]
    [InlineData("Daft Punk", "Daft Punk")]
    [InlineData("Daft Punk", "Daft Punk")]
    [InlineData("Daft​Punk", "Daft Punk")]
    public void NormalizeIndexedName_ShouldFoldNonBreakingAndZeroWidthSpaces(string input, string expected)
    {
        // Act
        string result = input.NormalizeIndexedName();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "NormalizeIndexedName should trim surrounding whitespace")]
    public void NormalizeIndexedName_ShouldTrimSurroundingWhitespace()
    {
        // Act
        string result = "  Pro-pain  ".NormalizeIndexedName();

        // Assert
        Assert.Equal("Pro-pain", result);
    }

    [Fact(DisplayName = "NormalizeIndexedName should compose decomposed unicode to NFC")]
    public void NormalizeIndexedName_ShouldComposeDecomposedUnicode_ToNfc()
    {
        // Built from char escapes so the source-file encoding cannot pre-normalize the input.
        string decomposed = "Béatrice";
        string composed = "Béatrice";

        Assert.NotEqual(composed, decomposed);
        Assert.Equal(composed, decomposed.NormalizeIndexedName());
    }

    [Theory(DisplayName = "NormalizeIndexedName should return input unchanged when null or empty")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void NormalizeIndexedName_ShouldReturnInputUnchanged_WhenNullOrEmpty(string input, string expected)
    {
        // Act
        string result = input.NormalizeIndexedName();

        // Assert
        Assert.Equal(expected, result);
    }
}
